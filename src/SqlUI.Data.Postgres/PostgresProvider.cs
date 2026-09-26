using System.Data;
using Npgsql;
using SqlUI.Core;
using SqlUI.Core.Models;

namespace SqlUI.Data.Postgres;

// NOT: Bu saglayici gercek bir PostgreSQL sunucusuna karsi test EDILMEDI (bu ortamda
// bir PostgreSQL ornegi yok). information_schema tabanli standart ANSI sorgular
// kullanilarak yazildi (SQL Server provider'i ile ayni desen), derleme-dogrulamasi
// yapildi ama calisma-zamani dogrulamasi kullanicinin kendi sunucusuyla yapilmali.
public class PostgresProvider : IDbProvider
{
    public string Key => "postgres";
    public string DisplayName => "PostgreSQL";
    public DbProviderCapabilities Capabilities =>
        DbProviderCapabilities.MultipleDatabases | DbProviderCapabilities.Schemas |
        DbProviderCapabilities.StoredProcedures | DbProviderCapabilities.ServerAuthentication;

    public string BuildConnectionString(ConnectionProfile profile, string? plainSecret)
    {
        var b = new NpgsqlConnectionStringBuilder
        {
            Host = profile.Server,
            Timeout = 15,
        };
        if (!string.IsNullOrEmpty(profile.Database)) b.Database = profile.Database;
        else b.Database = "postgres"; // baglanmak icin varsayilan sistem veritabani

        if (!profile.IntegratedSecurity)
        {
            b.Username = profile.UserName;
            b.Password = plainSecret;
        }
        return b.ConnectionString;
    }

    private static string WithDatabase(string connectionString, string? database)
    {
        if (string.IsNullOrEmpty(database)) return connectionString;
        var b = new NpgsqlConnectionStringBuilder(connectionString) { Database = database };
        return b.ConnectionString;
    }

    public async Task TestConnectionAsync(string connectionString, CancellationToken ct = default)
    {
        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetDatabasesAsync(string connectionString, CancellationToken ct = default)
    {
        var result = new List<string>();
        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        using var cmd = new NpgsqlCommand("SELECT datname FROM pg_database WHERE datistemplate = false ORDER BY datname", conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(string connectionString, string? database, CancellationToken ct = default)
        => await GetObjectsAsync(connectionString, database,
            "SELECT table_schema, table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE' AND table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY table_schema, table_name",
            isView: false, ct);

    public async Task<IReadOnlyList<TableInfo>> GetViewsAsync(string connectionString, string? database, CancellationToken ct = default)
        => await GetObjectsAsync(connectionString, database,
            "SELECT table_schema, table_name FROM information_schema.views WHERE table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY table_schema, table_name",
            isView: true, ct);

    private static async Task<IReadOnlyList<TableInfo>> GetObjectsAsync(string connectionString, string? database, string sql, bool isView, CancellationToken ct)
    {
        var result = new List<TableInfo>();
        using var conn = new NpgsqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new TableInfo { Schema = reader.GetString(0), Name = reader.GetString(1), IsView = isView });
        return result;
    }

    public async Task<IReadOnlyList<string>> GetStoredProceduresAsync(string connectionString, string? database, CancellationToken ct = default)
    {
        var result = new List<string>();
        using var conn = new NpgsqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        const string sql = "SELECT routine_name FROM information_schema.routines WHERE routine_type IN ('PROCEDURE', 'FUNCTION') AND routine_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY routine_name";
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string connectionString, string? database, string table, CancellationToken ct = default)
    {
        var result = new List<ColumnInfo>();
        using var conn = new NpgsqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        const string sql = @"
            SELECT c.column_name, c.data_type, c.is_nullable, c.character_maximum_length, c.numeric_precision, c.numeric_scale,
                   c.column_default LIKE 'nextval%' AS is_identity,
                   CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END AS is_pk
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT ku.table_name, ku.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage ku ON tc.constraint_name = ku.constraint_name
                WHERE tc.constraint_type = 'PRIMARY KEY'
            ) pk ON pk.table_name = c.table_name AND pk.column_name = c.column_name
            WHERE c.table_name = @table
            ORDER BY c.ordinal_position";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@table", table);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                MaxLength = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                NumericPrecision = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                NumericScale = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                IsIdentity = !reader.IsDBNull(6) && reader.GetBoolean(6),
                IsPrimaryKey = !reader.IsDBNull(7) && reader.GetBoolean(7),
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetAllColumnsAsync(string connectionString, string? database, CancellationToken ct = default)
    {
        var result = new List<ColumnInfo>();
        using var conn = new NpgsqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        const string sql = "SELECT table_name, column_name, data_type FROM information_schema.columns WHERE table_schema NOT IN ('pg_catalog', 'information_schema') ORDER BY table_name, ordinal_position";
        using var cmd = new NpgsqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new ColumnInfo { TableName = reader.GetString(0), Name = reader.GetString(1), DataType = reader.GetString(2) });
        return result;
    }

    public async Task<QueryExecutionResult> ExecuteQueryAsync(string connectionString, string? database, string sql, CancellationToken ct = default)
    {
        var result = new QueryExecutionResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var conn = new NpgsqlConnection(WithDatabase(connectionString, database));
        NoticeEventHandler onNotice = (s, e) => result.Messages.Add(e.Notice.MessageText);
        conn.Notice += onNotice;
        await conn.OpenAsync(ct);
        try
        {
            // Postgres'te GO batch ayraci yok; deyimler ';' ile ayrilir ve tek bir komutta calistirilabilir,
            // ama coklu result-set icin her deyimi ayri calistirip NpgsqlDataReader.NextResultAsync kullaniyoruz.
            using var cmd = new NpgsqlCommand(sql, conn) { CommandTimeout = 0 };
            using var reader = await cmd.ExecuteReaderAsync(ct);
            do
            {
                if (reader.FieldCount > 0)
                {
                    var dt = new DataTable();
                    for (int i = 0; i < reader.FieldCount; i++)
                        dt.Columns.Add(reader.GetName(i), reader.GetFieldType(i));
                    while (await reader.ReadAsync(ct))
                    {
                        var row = dt.NewRow();
                        for (int i = 0; i < reader.FieldCount; i++)
                            row[i] = await reader.IsDBNullAsync(i, ct) ? DBNull.Value : reader.GetValue(i);
                        dt.Rows.Add(row);
                    }
                    result.ResultSets.Add(dt);
                    result.Messages.Add($"({dt.Rows.Count} row(s) returned)");
                }
                else
                {
                    int affected = reader.RecordsAffected;
                    result.Messages.Add(affected >= 0 ? $"({affected} row(s) affected)" : "Command(s) completed successfully.");
                }
            } while (await reader.NextResultAsync(ct));
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            throw new OperationCanceledException("Sorgu iptal edildi.", ct);
        }
        finally { conn.Notice -= onNotice; }

        sw.Stop();
        result.Elapsed = sw.Elapsed;
        return result;
    }

    public async Task UpdateCellAsync(string connectionString, string? database, string table, string column, object? newValue,
        string pkColumn, object? pkValue, CancellationToken ct = default)
    {
        using var conn = new NpgsqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        string sql = $"UPDATE {QuoteIdentifier(table)} SET {QuoteIdentifier(column)} = @val WHERE {QuoteIdentifier(pkColumn)} = @pk";
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@val", newValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pk", pkValue ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    public string BuildSelectTopQuery(string table, int rowLimit) => $"SELECT * FROM {QuoteIdentifier(table)} LIMIT {rowLimit}";

    public Task<string?> GetEstimatedPlanXmlAsync(string connectionString, string? database, string sql, CancellationToken ct = default)
        => throw new NotSupportedException("PostgreSQL icin yurutme plani gosterimi bu surumde desteklenmiyor.");

    // DatabaseManagement capability'si isaretli degil (backup/restore icin pg_dump gibi harici
    // araclar gerekir, salt SQL ile yapilamaz) - bu metodlar UI'dan hic cagirilmamali.
    public Task CreateDatabaseAsync(string connectionString, string database, CancellationToken ct = default)
        => throw new NotSupportedException("Bu surumde desteklenmiyor.");
    public Task RenameDatabaseAsync(string connectionString, string oldName, string newName, CancellationToken ct = default)
        => throw new NotSupportedException("Bu surumde desteklenmiyor.");
    public Task DeleteDatabaseAsync(string connectionString, string database, CancellationToken ct = default)
        => throw new NotSupportedException("Bu surumde desteklenmiyor.");
    public Task BackupDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default)
        => throw new NotSupportedException("PostgreSQL yedekleme icin pg_dump gerekir, bu surumde desteklenmiyor.");
    public Task RestoreDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default)
        => throw new NotSupportedException("PostgreSQL geri yukleme icin pg_restore gerekir, bu surumde desteklenmiyor.");

    public async Task DeleteTableAsync(string connectionString, string? database, string table, CancellationToken ct = default)
    {
        using var conn = new NpgsqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        using var cmd = new NpgsqlCommand($"DROP TABLE {QuoteIdentifier(table)}", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public Task<string?> GetStoredProcedureDefinitionAsync(string connectionString, string? database, string spName, CancellationToken ct = default)
        => throw new NotSupportedException("Bu surumde desteklenmiyor.");
    public Task DeleteStoredProcedureAsync(string connectionString, string? database, string spName, CancellationToken ct = default)
        => throw new NotSupportedException("Bu surumde desteklenmiyor.");
    public Task<IReadOnlyList<(string Name, string DataType, bool IsOutput)>> GetStoredProcedureParametersAsync(
        string connectionString, string? database, string spName, CancellationToken ct = default)
        => throw new NotSupportedException("Bu surumde desteklenmiyor.");
}
