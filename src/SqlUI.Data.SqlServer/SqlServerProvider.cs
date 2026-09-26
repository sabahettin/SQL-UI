using System.Data;
using Microsoft.Data.SqlClient;
using SqlUI.Core;
using SqlUI.Core.Models;

namespace SqlUI.Data.SqlServer;

public class SqlServerProvider : IDbProvider
{
    public string Key => "sqlserver";
    public string DisplayName => "SQL Server";
    public DbProviderCapabilities Capabilities =>
        DbProviderCapabilities.MultipleDatabases | DbProviderCapabilities.Schemas |
        DbProviderCapabilities.StoredProcedures | DbProviderCapabilities.ExecutionPlan |
        DbProviderCapabilities.ServerAuthentication | DbProviderCapabilities.DatabaseManagement;

    private static readonly HashSet<string> SystemDatabaseNames =
        new(StringComparer.OrdinalIgnoreCase) { "master", "model", "msdb", "tempdb" };

    public string BuildConnectionString(ConnectionProfile profile, string? plainSecret)
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = profile.Server,
            TrustServerCertificate = true,
            ConnectTimeout = 15,
        };
        if (!string.IsNullOrEmpty(profile.Database)) b.InitialCatalog = profile.Database;

        if (profile.IntegratedSecurity) b.IntegratedSecurity = true;
        else
        {
            b.UserID = profile.UserName ?? "";
            b.Password = plainSecret ?? "";
        }
        return b.ConnectionString;
    }

    // database verildiginde InitialCatalog'u degistirilmis yeni bir connection string doner.
    private static string WithDatabase(string connectionString, string? database)
    {
        if (string.IsNullOrEmpty(database)) return connectionString;
        var b = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = database };
        return b.ConnectionString;
    }

    public async Task TestConnectionAsync(string connectionString, CancellationToken ct = default)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetDatabasesAsync(string connectionString, CancellationToken ct = default)
    {
        var result = new List<string>();
        using var conn = new SqlConnection(WithDatabase(connectionString, "master"));
        await conn.OpenAsync(ct);
        using var cmd = new SqlCommand("SELECT name FROM sys.databases WHERE state_desc = 'ONLINE' ORDER BY name", conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(string connectionString, string? database, CancellationToken ct = default)
        => await GetObjectsAsync(connectionString, database,
            "SELECT s.name AS SchemaName, t.name AS TableName FROM sys.tables t JOIN sys.schemas s ON t.schema_id = s.schema_id ORDER BY s.name, t.name",
            isView: false, ct);

    public async Task<IReadOnlyList<TableInfo>> GetViewsAsync(string connectionString, string? database, CancellationToken ct = default)
        => await GetObjectsAsync(connectionString, database,
            "SELECT s.name AS SchemaName, v.name AS TableName FROM sys.views v JOIN sys.schemas s ON v.schema_id = s.schema_id ORDER BY s.name, v.name",
            isView: true, ct);

    private static async Task<IReadOnlyList<TableInfo>> GetObjectsAsync(string connectionString, string? database, string sql, bool isView, CancellationToken ct)
    {
        var result = new List<TableInfo>();
        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new TableInfo { Schema = reader.GetString(0), Name = reader.GetString(1), IsView = isView });
        return result;
    }

    public async Task<IReadOnlyList<string>> GetStoredProceduresAsync(string connectionString, string? database, CancellationToken ct = default)
    {
        var result = new List<string>();
        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        using var cmd = new SqlCommand("SELECT name FROM sys.procedures ORDER BY name", conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string connectionString, string? database, string table, CancellationToken ct = default)
    {
        var result = new List<ColumnInfo>();
        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        const string sql = @"
            SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE, c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE,
                   CAST(COLUMNPROPERTY(OBJECT_ID(c.TABLE_SCHEMA + '.' + c.TABLE_NAME), c.COLUMN_NAME, 'IsIdentity') AS bit) AS IS_IDENTITY,
                   CAST(CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS bit) AS IS_PK
            FROM INFORMATION_SCHEMA.COLUMNS c
            LEFT JOIN (
                SELECT ku.TABLE_SCHEMA, ku.TABLE_NAME, ku.COLUMN_NAME
                FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
            ) pk ON pk.TABLE_SCHEMA = c.TABLE_SCHEMA AND pk.TABLE_NAME = c.TABLE_NAME AND pk.COLUMN_NAME = c.COLUMN_NAME
            WHERE c.TABLE_NAME = @table
            ORDER BY c.ORDINAL_POSITION";
        using var cmd = new SqlCommand(sql, conn);
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
                NumericPrecision = reader.IsDBNull(4) ? null : (int?)reader.GetByte(4),
                NumericScale = reader.IsDBNull(5) ? null : (int?)reader.GetInt32(5),
                IsIdentity = !reader.IsDBNull(6) && reader.GetBoolean(6),
                IsPrimaryKey = !reader.IsDBNull(7) && reader.GetBoolean(7),
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetAllColumnsAsync(string connectionString, string? database, CancellationToken ct = default)
    {
        var result = new List<ColumnInfo>();
        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        const string sql = "SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS ORDER BY TABLE_NAME, ORDINAL_POSITION";
        using var cmd = new SqlCommand(sql, conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add(new ColumnInfo { TableName = reader.GetString(0), Name = reader.GetString(1), DataType = reader.GetString(2) });
        return result;
    }

    public async Task<QueryExecutionResult> ExecuteQueryAsync(string connectionString, string? database, string sql, CancellationToken ct = default)
    {
        var result = new QueryExecutionResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var batches = System.Text.RegularExpressions.Regex.Split(sql, @"^\s*GO\s*$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        SqlInfoMessageEventHandler onInfoMessage = (s, e) => result.Messages.Add(e.Message);
        conn.InfoMessage += onInfoMessage;
        await conn.OpenAsync(ct);
        try
        {
            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch)) continue;
                ct.ThrowIfCancellationRequested();
                using var cmd = new SqlCommand(batch, conn) { CommandTimeout = 0 };
                using var reader = await cmd.ExecuteReaderAsync(ct);
                do
                {
                    if (reader.FieldCount > 0)
                    {
                        // DataTable.Load(reader) reader'i kendi ici bitirince kapatir; bu da
                        // sonraki NextResultAsync cagrisinin "reader kapali" hatasiyla patlamasina
                        // yol acar. Satirlari elle okuyup reader'in acik kalmasini sagliyoruz.
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
        }
        catch (Exception) when (ct.IsCancellationRequested)
        {
            // Microsoft.Data.SqlClient, CancellationToken uzerinden komut iptal edildiginde
            // OperationCanceledException degil "Operation cancelled by user" mesajli bir
            // SqlException firlatiyor. Cagiran taraf (UI) her zaman OperationCanceledException
            // bekleyebilsin diye burada normallestiriyoruz.
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
        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        string sql = $"UPDATE {QuoteIdentifier(table)} SET {QuoteIdentifier(column)} = @val WHERE {QuoteIdentifier(pkColumn)} = @pk";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@val", newValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pk", pkValue ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public string QuoteIdentifier(string identifier) => $"[{identifier}]";

    public string BuildSelectTopQuery(string table, int rowLimit) => $"SELECT TOP {rowLimit} * FROM {QuoteIdentifier(table)}";

    public async Task<string?> GetEstimatedPlanXmlAsync(string connectionString, string? database, string sql, CancellationToken ct = default)
    {
        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        using (var onCmd = new SqlCommand("SET SHOWPLAN_XML ON", conn)) await onCmd.ExecuteNonQueryAsync(ct);
        try
        {
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct)) return reader.GetString(0);
            return null;
        }
        finally
        {
            using var offCmd = new SqlCommand("SET SHOWPLAN_XML OFF", conn);
            await offCmd.ExecuteNonQueryAsync(ct);
        }
    }

    public async Task CreateDatabaseAsync(string connectionString, string database, CancellationToken ct = default)
    {
        using var conn = new SqlConnection(WithDatabase(connectionString, "master"));
        await conn.OpenAsync(ct);
        using var cmd = new SqlCommand($"CREATE DATABASE {QuoteIdentifier(database)}", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RenameDatabaseAsync(string connectionString, string oldName, string newName, CancellationToken ct = default)
    {
        if (SystemDatabaseNames.Contains(oldName)) throw new InvalidOperationException("Sistem veritabanlari yeniden adlandirilamaz.");
        using var conn = new SqlConnection(WithDatabase(connectionString, "master"));
        await conn.OpenAsync(ct);
        string sql = $"ALTER DATABASE {QuoteIdentifier(oldName)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                     $"ALTER DATABASE {QuoteIdentifier(oldName)} MODIFY NAME = {QuoteIdentifier(newName)}; " +
                     $"ALTER DATABASE {QuoteIdentifier(newName)} SET MULTI_USER;";
        using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteDatabaseAsync(string connectionString, string database, CancellationToken ct = default)
    {
        if (SystemDatabaseNames.Contains(database)) throw new InvalidOperationException("Sistem veritabanlari silinemez.");
        using var conn = new SqlConnection(WithDatabase(connectionString, "master"));
        await conn.OpenAsync(ct);
        string sql = $"ALTER DATABASE {QuoteIdentifier(database)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {QuoteIdentifier(database)};";
        using var cmd = new SqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task BackupDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default)
    {
        using var conn = new SqlConnection(WithDatabase(connectionString, "master"));
        await conn.OpenAsync(ct);
        string sql = $"BACKUP DATABASE {QuoteIdentifier(database)} TO DISK = @path WITH FORMAT, INIT";
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };
        cmd.Parameters.AddWithValue("@path", filePath);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task RestoreDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default)
    {
        using var conn = new SqlConnection(WithDatabase(connectionString, "master"));
        await conn.OpenAsync(ct);
        string sql = $"ALTER DATABASE {QuoteIdentifier(database)} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                     $"RESTORE DATABASE {QuoteIdentifier(database)} FROM DISK = @path WITH REPLACE; " +
                     $"ALTER DATABASE {QuoteIdentifier(database)} SET MULTI_USER;";
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 0 };
        cmd.Parameters.AddWithValue("@path", filePath);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteTableAsync(string connectionString, string? database, string table, CancellationToken ct = default)
    {
        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        using var cmd = new SqlCommand($"DROP TABLE {QuoteIdentifier(table)}", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<string?> GetStoredProcedureDefinitionAsync(string connectionString, string? database, string spName, CancellationToken ct = default)
    {
        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        using var cmd = new SqlCommand("SELECT OBJECT_DEFINITION(OBJECT_ID(@spName))", conn);
        cmd.Parameters.AddWithValue("@spName", spName);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result as string;
    }

    public async Task DeleteStoredProcedureAsync(string connectionString, string? database, string spName, CancellationToken ct = default)
    {
        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        using var cmd = new SqlCommand($"DROP PROCEDURE {QuoteIdentifier(spName)}", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<(string Name, string DataType, bool IsOutput)>> GetStoredProcedureParametersAsync(
        string connectionString, string? database, string spName, CancellationToken ct = default)
    {
        var result = new List<(string, string, bool)>();
        using var conn = new SqlConnection(WithDatabase(connectionString, database));
        await conn.OpenAsync(ct);
        const string sql = @"
            SELECT p.name, t.name, p.is_output
            FROM sys.parameters p
            JOIN sys.types t ON p.user_type_id = t.user_type_id
            WHERE p.object_id = OBJECT_ID(@spName)
            ORDER BY p.parameter_id";
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@spName", spName);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            result.Add((reader.GetString(0), reader.GetString(1), reader.GetBoolean(2)));
        return result;
    }
}
