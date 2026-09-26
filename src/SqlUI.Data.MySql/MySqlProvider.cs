using System.Data;
using MySqlConnector;
using SqlUI.Core;
using SqlUI.Core.Models;

namespace SqlUI.Data.MySql;

// NOT: Bu saglayici gercek bir MySQL/MariaDB sunucusuna karsi test EDILMEDI (bu ortamda
// bir ornegi yok). information_schema tabanli standart sorgular kullanilarak yazildi,
// derleme-dogrulamasi yapildi ama calisma-zamani dogrulamasi kullanicinin kendi
// sunucusuyla yapilmali. MySQL'de "schema" ve "database" ayni kavramdir (SQL Server'daki
// gibi ayri bir sema seviyesi yoktur), bu yuzden Capabilities.Schemas isaretlenmedi.
public class MySqlProvider : IDbProvider
{
    public string Key => "mysql";
    public string DisplayName => "MySQL / MariaDB";
    public DbProviderCapabilities Capabilities =>
        DbProviderCapabilities.MultipleDatabases | DbProviderCapabilities.StoredProcedures |
        DbProviderCapabilities.ServerAuthentication;

    public string BuildConnectionString(ConnectionProfile profile, string? plainSecret)
    {
        var b = new MySqlConnectionStringBuilder
        {
            Server = profile.Server,
            ConnectionTimeout = 15,
        };
        if (!string.IsNullOrEmpty(profile.Database)) b.Database = profile.Database;

        if (!profile.IntegratedSecurity)
        {
            b.UserID = profile.UserName ?? "";
            b.Password = plainSecret ?? "";
        }
        return b.ConnectionString;
    }

    private static string WithDatabase(string connectionString, string? database)
    {
        if (string.IsNullOrEmpty(database)) return connectionString;
        var b = new MySqlConnectionStringBuilder(connectionString) { Database = database };
        return b.ConnectionString;
    }

    public async Task TestConnectionAsync(string connectionString, CancellationToken ct = default)
    {
        using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetDatabasesAsync(string connectionString, CancellationToken ct = default)
    {
        var result = new List<string>();
        using var conn = new MySqlConnection(connectionString);
        await conn.OpenAsync(ct);
        const string sql = "SELECT schema_name FROM information_schema.schemata WHERE schema_name NOT IN ('information_schema','mysql','performance_schema','sys') ORDER BY schema_name";
        using var cmd = new MySqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(string connectionString, string? database, CancellationToken ct = default)
        => await GetObjectsAsync(connectionString, database, "BASE TABLE", isView: false, ct);

    public async Task<IReadOnlyList<TableInfo>> GetViewsAsync(string connectionString, string? database, CancellationToken ct = default)
        => await GetObjectsAsync(connectionString, database, "VIEW", isView: true, ct);

    private static async Task<IReadOnlyList<TableInfo>> GetObjectsAsync(string connectionString, string? database, string tableType, bool isView, CancellationToken ct)
    {
        var result = new List<TableInfo>();
        using var conn = new MySqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        const string sql = "SELECT table_name FROM information_schema.tables WHERE table_schema = DATABASE() AND table_type = @type ORDER BY table_name";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@type", tableType);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new TableInfo { Name = reader.GetString(0), IsView = isView });
        return result;
    }

    public async Task<IReadOnlyList<string>> GetStoredProceduresAsync(string connectionString, string? database, CancellationToken ct = default)
    {
        var result = new List<string>();
        using var conn = new MySqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        const string sql = "SELECT routine_name FROM information_schema.routines WHERE routine_schema = DATABASE() ORDER BY routine_name";
        using var cmd = new MySqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string connectionString, string? database, string table, CancellationToken ct = default)
    {
        var result = new List<ColumnInfo>();
        using var conn = new MySqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        const string sql = @"
            SELECT column_name, data_type, is_nullable, character_maximum_length, numeric_precision, numeric_scale,
                   (extra LIKE '%auto_increment%') AS is_identity,
                   (column_key = 'PRI') AS is_pk
            FROM information_schema.columns
            WHERE table_schema = DATABASE() AND table_name = @table
            ORDER BY ordinal_position";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@table", table);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ColumnInfo
            {
                Name = reader.GetString(0),
                DataType = reader.GetString(1),
                IsNullable = reader.GetString(2) == "YES",
                MaxLength = reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
                NumericPrecision = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                NumericScale = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
                IsIdentity = !reader.IsDBNull(6) && Convert.ToBoolean(reader.GetValue(6)),
                IsPrimaryKey = !reader.IsDBNull(7) && Convert.ToBoolean(reader.GetValue(7)),
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetAllColumnsAsync(string connectionString, string? database, CancellationToken ct = default)
    {
        var result = new List<ColumnInfo>();
        using var conn = new MySqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        const string sql = "SELECT table_name, column_name, data_type FROM information_schema.columns WHERE table_schema = DATABASE() ORDER BY table_name, ordinal_position";
        using var cmd = new MySqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new ColumnInfo { TableName = reader.GetString(0), Name = reader.GetString(1), DataType = reader.GetString(2) });
        return result;
    }

    public async Task<QueryExecutionResult> ExecuteQueryAsync(string connectionString, string? database, string sql, CancellationToken ct = default)
    {
        var result = new QueryExecutionResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var conn = new MySqlConnection(WithDatabase(connectionString, database));
        MySqlInfoMessageEventHandler onInfoMessage = (s, e) => { foreach (var err in e.Errors) result.Messages.Add(err.Message); };
        conn.InfoMessage += onInfoMessage;
        await conn.OpenAsync(ct);
        try
        {
            using var cmd = new MySqlCommand(sql, conn) { CommandTimeout = 0 };
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
        finally { conn.InfoMessage -= onInfoMessage; }

        sw.Stop();
        result.Elapsed = sw.Elapsed;
        return result;
    }

    public async Task UpdateCellAsync(string connectionString, string? database, string table, string column, object? newValue,
        string pkColumn, object? pkValue, CancellationToken ct = default)
    {
        using var conn = new MySqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        string sql = $"UPDATE {QuoteIdentifier(table)} SET {QuoteIdentifier(column)} = @val WHERE {QuoteIdentifier(pkColumn)} = @pk";
        using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@val", newValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pk", pkValue ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public string QuoteIdentifier(string identifier) => $"`{identifier.Replace("`", "``")}`";

    public string BuildSelectTopQuery(string table, int rowLimit) => $"SELECT * FROM {QuoteIdentifier(table)} LIMIT {rowLimit}";

    public Task<string?> GetEstimatedPlanXmlAsync(string connectionString, string? database, string sql, CancellationToken ct = default)
        => throw new NotSupportedException("MySQL icin yurutme plani gosterimi bu surumde desteklenmiyor.");

    // DatabaseManagement capability'si isaretli degil - bu metodlar UI'dan hic cagirilmamali.
    public Task CreateDatabaseAsync(string connectionString, string database, CancellationToken ct = default)
        => throw new NotSupportedException("Bu surumde desteklenmiyor.");
    public Task RenameDatabaseAsync(string connectionString, string oldName, string newName, CancellationToken ct = default)
        => throw new NotSupportedException("MySQL'de dogrudan veritabani yeniden adlandirma yok.");
    public Task DeleteDatabaseAsync(string connectionString, string database, CancellationToken ct = default)
        => throw new NotSupportedException("Bu surumde desteklenmiyor.");
    public Task BackupDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default)
        => throw new NotSupportedException("MySQL yedekleme icin mysqldump gerekir, bu surumde desteklenmiyor.");
    public Task RestoreDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default)
        => throw new NotSupportedException("Bu surumde desteklenmiyor.");

    public async Task DeleteTableAsync(string connectionString, string? database, string table, CancellationToken ct = default)
    {
        using var conn = new MySqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        using var cmd = new MySqlCommand($"DROP TABLE {QuoteIdentifier(table)}", conn);
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
