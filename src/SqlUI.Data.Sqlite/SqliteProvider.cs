using System.Data;
using Microsoft.Data.Sqlite;
using SqlUI.Core;
using SqlUI.Core.Models;

namespace SqlUI.Data.Sqlite;

public class SqliteProvider : IDbProvider
{
    public string Key => "sqlite";
    public string DisplayName => "SQLite";
    public DbProviderCapabilities Capabilities => DbProviderCapabilities.None; // dosya = veritabani, sema/SP/plan yok

    public string BuildConnectionString(ConnectionProfile profile, string? plainSecret)
        => new SqliteConnectionStringBuilder { DataSource = profile.Server }.ConnectionString;

    public async Task TestConnectionAsync(string connectionString, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
    }

    // SQLite'ta "veritabani" kavrami dosyanin kendisidir; sunucu tarzi bir liste yoktur.
    public Task<IReadOnlyList<string>> GetDatabasesAsync(string connectionString, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public async Task<IReadOnlyList<TableInfo>> GetTablesAsync(string connectionString, string? database, CancellationToken ct = default)
        => await GetObjectsAsync(connectionString, "table", isView: false, ct);

    public async Task<IReadOnlyList<TableInfo>> GetViewsAsync(string connectionString, string? database, CancellationToken ct = default)
        => await GetObjectsAsync(connectionString, "view", isView: true, ct);

    private static async Task<IReadOnlyList<TableInfo>> GetObjectsAsync(string connectionString, string type, bool isView, CancellationToken ct)
    {
        var result = new List<TableInfo>();
        using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
        using var cmd = new SqliteCommand("SELECT name FROM sqlite_master WHERE type = @type AND name NOT LIKE 'sqlite_%' ORDER BY name", conn);
        cmd.Parameters.AddWithValue("@type", type);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) result.Add(new TableInfo { Name = reader.GetString(0), IsView = isView });
        return result;
    }

    // SQLite'ta stored procedure kavrami yok.
    public Task<IReadOnlyList<string>> GetStoredProceduresAsync(string connectionString, string? database, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public async Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string connectionString, string? database, string table, CancellationToken ct = default)
    {
        var result = new List<ColumnInfo>();
        using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
        // PRAGMA parametre kabul etmez; tablo adi GetTablesAsync'ten geldigi icin guvenlidir, yine de tirnaklanir.
        using var cmd = new SqliteCommand($"PRAGMA table_info({QuoteIdentifier(table)})", conn);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            result.Add(new ColumnInfo
            {
                Name = reader.GetString(reader.GetOrdinal("name")),
                DataType = reader.IsDBNull(reader.GetOrdinal("type")) ? "" : reader.GetString(reader.GetOrdinal("type")),
                IsNullable = reader.GetInt64(reader.GetOrdinal("notnull")) == 0,
                IsPrimaryKey = reader.GetInt64(reader.GetOrdinal("pk")) > 0,
            });
        }
        return result;
    }

    public async Task<IReadOnlyList<ColumnInfo>> GetAllColumnsAsync(string connectionString, string? database, CancellationToken ct = default)
    {
        // SQLite'ta tum kolonlari tek sorguda almanin (pragma_table_info tablo-degerli
        // fonksiyonu) surum bagimliligi var; personal .db dosyalari kucuk oldugu icin
        // tablo basina PRAGMA cagirmak basit ve guvenilir.
        var result = new List<ColumnInfo>();
        var tables = await GetTablesAsync(connectionString, database, ct);
        foreach (var table in tables)
        {
            var columns = await GetColumnsAsync(connectionString, database, table.Name, ct);
            foreach (var c in columns) { c.TableName = table.Name; result.Add(c); }
        }
        return result;
    }

    public async Task<QueryExecutionResult> ExecuteQueryAsync(string connectionString, string? database, string sql, CancellationToken ct = default)
    {
        var result = new QueryExecutionResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var statements = sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
        foreach (var statement in statements)
        {
            if (string.IsNullOrWhiteSpace(statement)) continue;
            ct.ThrowIfCancellationRequested();
            using var cmd = new SqliteCommand(statement, conn);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (reader.FieldCount > 0)
            {
                var dt = new DataTable();
                dt.Load(reader);
                result.ResultSets.Add(dt);
                result.Messages.Add($"({dt.Rows.Count} row(s) returned)");
            }
            else
            {
                result.Messages.Add($"({reader.RecordsAffected} row(s) affected)");
            }
        }

        sw.Stop();
        result.Elapsed = sw.Elapsed;
        return result;
    }

    public async Task UpdateCellAsync(string connectionString, string? database, string table, string column, object? newValue,
        string pkColumn, object? pkValue, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
        string sql = $"UPDATE {QuoteIdentifier(table)} SET {QuoteIdentifier(column)} = @val WHERE {QuoteIdentifier(pkColumn)} = @pk";
        using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@val", newValue ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pk", pkValue ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public string QuoteIdentifier(string identifier) => $"\"{identifier.Replace("\"", "\"\"")}\"";

    public string BuildSelectTopQuery(string table, int rowLimit) => $"SELECT * FROM {QuoteIdentifier(table)} LIMIT {rowLimit}";

    // SQLite icin SET SHOWPLAN_XML muadili yok; Capabilities'de ExecutionPlan isaretli olmadigi
    // icin UI bu metodu hic cagirmamali. Yine de arayuz sozlesmesi geregi burada.
    public Task<string?> GetEstimatedPlanXmlAsync(string connectionString, string? database, string sql, CancellationToken ct = default)
        => throw new NotSupportedException("SQLite icin yurutme plani desteklenmiyor.");

    // SQLite'ta "veritabani" tek bir dosyadir; Capabilities'de DatabaseManagement isaretli
    // olmadigi icin UI bu metodlari (Backup/Restore haric) hic cagirmamali.
    public Task CreateDatabaseAsync(string connectionString, string database, CancellationToken ct = default)
        => throw new NotSupportedException("SQLite'ta 'veritabani olustur' kavrami yok - Baglan penceresinden yeni bir dosya secin.");

    public Task RenameDatabaseAsync(string connectionString, string oldName, string newName, CancellationToken ct = default)
        => throw new NotSupportedException("SQLite'ta veritabani yeniden adlandirma desteklenmiyor - dosyayi Gezgin'den yeniden adlandirin.");

    public Task DeleteDatabaseAsync(string connectionString, string database, CancellationToken ct = default)
        => throw new NotSupportedException("SQLite'ta veritabani silme desteklenmiyor - dosyayi Gezgin'den silin.");

    // SQLite bir dosya oldugu icin "backup" == dosya kopyalama. Baglantiyi kapatip kopyalamak
    // yerine SQLite'in kendi .backup API'sini kullanmak daha guvenli (kopyalama sirasinda yazma
    // olursa bozulma riski dusuk olur).
    public async Task BackupDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default)
    {
        using var source = new SqliteConnection(connectionString);
        await source.OpenAsync(ct);
        using var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = filePath }.ConnectionString);
        await destination.OpenAsync(ct);
        source.BackupDatabase(destination);
    }

    public Task RestoreDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default)
        => throw new NotSupportedException("SQLite'ta geri yukleme icin Baglan penceresinden dogrudan .bak dosyasini acin.");

    public async Task DeleteTableAsync(string connectionString, string? database, string table, CancellationToken ct = default)
    {
        using var conn = new SqliteConnection(connectionString);
        await conn.OpenAsync(ct);
        using var cmd = new SqliteCommand($"DROP TABLE {QuoteIdentifier(table)}", conn);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // SQLite'ta stored procedure kavrami yok (Capabilities.StoredProcedures isaretli degil).
    public Task<string?> GetStoredProcedureDefinitionAsync(string connectionString, string? database, string spName, CancellationToken ct = default)
        => throw new NotSupportedException("SQLite'ta saklanan yordam yok.");

    public Task DeleteStoredProcedureAsync(string connectionString, string? database, string spName, CancellationToken ct = default)
        => throw new NotSupportedException("SQLite'ta saklanan yordam yok.");

    public Task<IReadOnlyList<(string Name, string DataType, bool IsOutput)>> GetStoredProcedureParametersAsync(
        string connectionString, string? database, string spName, CancellationToken ct = default)
        => throw new NotSupportedException("SQLite'ta saklanan yordam yok.");
}
