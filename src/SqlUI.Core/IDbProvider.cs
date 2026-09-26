using SqlUI.Core.Models;

namespace SqlUI.Core;

// Her veritabani motoru (SQL Server, SQLite, ileride Postgres/MySQL) bu arayuzu implemente eder.
// UI katmani hicbir zaman Microsoft.Data.SqlClient / Microsoft.Data.Sqlite gibi somut turlere
// dogrudan bagli olmaz; sadece IDbProvider uzerinden konusur.
public interface IDbProvider
{
    string Key { get; }              // "sqlserver", "sqlite" ...
    string DisplayName { get; }      // "SQL Server", "SQLite"
    DbProviderCapabilities Capabilities { get; }

    string BuildConnectionString(ConnectionProfile profile, string? plainSecret);

    Task TestConnectionAsync(string connectionString, CancellationToken ct = default);

    // database == null ise saglayicinin varsayilan/aktif veritabani kullanilir (SQLite'ta hep null).
    Task<IReadOnlyList<string>> GetDatabasesAsync(string connectionString, CancellationToken ct = default);
    Task<IReadOnlyList<TableInfo>> GetTablesAsync(string connectionString, string? database, CancellationToken ct = default);
    Task<IReadOnlyList<TableInfo>> GetViewsAsync(string connectionString, string? database, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetStoredProceduresAsync(string connectionString, string? database, CancellationToken ct = default);
    Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string connectionString, string? database, string table, CancellationToken ct = default);

    // IntelliSense icin: veritabanindaki tum tablo/kolon ciftlerini TEK sorguda doner
    // (tablo basina ayri sorgu yerine). ColumnInfo.TableName burada her zaman dolu gelir.
    Task<IReadOnlyList<ColumnInfo>> GetAllColumnsAsync(string connectionString, string? database, CancellationToken ct = default);

    Task<QueryExecutionResult> ExecuteQueryAsync(string connectionString, string? database, string sql, CancellationToken ct = default);

    // Sonuc grid'inde hucre duzenleme icin: "SELECT TOP N * FROM table" gibi basit, tek-tabloluk
    // sorgularda kullanilir (bkz. QueryTabState.PendingEditTable / ResultSetViewModel.EditableTable).
    Task UpdateCellAsync(string connectionString, string? database, string table, string column, object? newValue,
        string pkColumn, object? pkValue, CancellationToken ct = default);

    string QuoteIdentifier(string identifier);

    // "SELECT TOP N * FROM table" T-SQL'e ozeldir, SQLite/Postgres/MySQL farkli sozdizimi
    // kullanir (orn. LIMIT). Object Explorer'da tabloya cift-tik gibi "ilk N satiri goster"
    // eylemleri bu metodu kullanmali, sorguyu elle kurmamalidir.
    string BuildSelectTopQuery(string table, int rowLimit);

    // Yalnizca Capabilities.ExecutionPlan isaretliyken cagrilir. Tahmini plan - sorguyu
    // gercekten CALISTIRMAZ (SQL Server'da SET SHOWPLAN_XML ON tum sonraki deyimleri
    // yalnizca planlar, INSERT/UPDATE/DELETE dahil hicbirini uygulamaz), bu yuzden guvenlidir.
    Task<string?> GetEstimatedPlanXmlAsync(string connectionString, string? database, string sql, CancellationToken ct = default);

    // Asagidakiler yalnizca Capabilities.DatabaseManagement isaretliyken UI'da gosterilir/cagrilir
    // (su an sadece SQL Server). Desteklemeyen saglayicilar NotSupportedException firlatabilir.
    Task CreateDatabaseAsync(string connectionString, string database, CancellationToken ct = default);
    Task RenameDatabaseAsync(string connectionString, string oldName, string newName, CancellationToken ct = default);
    Task DeleteDatabaseAsync(string connectionString, string database, CancellationToken ct = default);
    Task BackupDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default);
    Task RestoreDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default);

    // Tablo/SP silme - tum saglayicilarda calisir (identifier quoting disinda motor-bagimsiz DDL).
    Task DeleteTableAsync(string connectionString, string? database, string table, CancellationToken ct = default);
    Task<string?> GetStoredProcedureDefinitionAsync(string connectionString, string? database, string spName, CancellationToken ct = default);
    Task DeleteStoredProcedureAsync(string connectionString, string? database, string spName, CancellationToken ct = default);

    // Saklanan yordamin parametrelerini doner: (ParameterName, DataType, IsOutput).
    Task<IReadOnlyList<(string Name, string DataType, bool IsOutput)>> GetStoredProcedureParametersAsync(
        string connectionString, string? database, string spName, CancellationToken ct = default);
}
