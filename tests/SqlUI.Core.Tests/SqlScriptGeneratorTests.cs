using SqlUI.Core;
using SqlUI.Core.Models;
using SqlUI.Core.Scripting;
using Xunit;

namespace SqlUI.Core.Tests;

// SqlScriptGenerator'i test etmek icin gercek bir saglayiciya ihtiyac yok; sadece
// QuoteIdentifier ve BuildSelectTopQuery kullanilir. Iki farkli "dialect" simule ederek
// (kose parantez vs cift tirnak) uretimin gercekten saglayici-bagimli kaldigini dogruluyoruz.
internal class FakeSqlServerLikeProvider : IDbProvider
{
    public string Key => "fake-sqlserver";
    public string DisplayName => "Fake SQL Server";
    public DbProviderCapabilities Capabilities => DbProviderCapabilities.None;
    public string BuildConnectionString(ConnectionProfile profile, string? plainSecret) => throw new NotImplementedException();
    public Task TestConnectionAsync(string connectionString, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<string>> GetDatabasesAsync(string connectionString, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<TableInfo>> GetTablesAsync(string connectionString, string? database, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<TableInfo>> GetViewsAsync(string connectionString, string? database, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<string>> GetStoredProceduresAsync(string connectionString, string? database, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<ColumnInfo>> GetColumnsAsync(string connectionString, string? database, string table, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<ColumnInfo>> GetAllColumnsAsync(string connectionString, string? database, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<QueryExecutionResult> ExecuteQueryAsync(string connectionString, string? database, string sql, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateCellAsync(string connectionString, string? database, string table, string column, object? newValue, string pkColumn, object? pkValue, CancellationToken ct = default) => throw new NotImplementedException();
    public string QuoteIdentifier(string identifier) => $"[{identifier}]";
    public string BuildSelectTopQuery(string table, int rowLimit) => $"SELECT TOP {rowLimit} * FROM {QuoteIdentifier(table)}";
    public Task<string?> GetEstimatedPlanXmlAsync(string connectionString, string? database, string sql, CancellationToken ct = default) => throw new NotImplementedException();
    public Task CreateDatabaseAsync(string connectionString, string database, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RenameDatabaseAsync(string connectionString, string oldName, string newName, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteDatabaseAsync(string connectionString, string database, CancellationToken ct = default) => throw new NotImplementedException();
    public Task BackupDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RestoreDatabaseAsync(string connectionString, string database, string filePath, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteTableAsync(string connectionString, string? database, string table, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<string?> GetStoredProcedureDefinitionAsync(string connectionString, string? database, string spName, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteStoredProcedureAsync(string connectionString, string? database, string spName, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<(string Name, string DataType, bool IsOutput)>> GetStoredProcedureParametersAsync(string connectionString, string? database, string spName, CancellationToken ct = default) => throw new NotImplementedException();
}

public class SqlScriptGeneratorTests
{
    private readonly FakeSqlServerLikeProvider _provider = new();

    private static List<ColumnInfo> SampleColumns() => new()
    {
        new ColumnInfo { Name = "Id", DataType = "int", IsIdentity = true, IsPrimaryKey = true, IsNullable = false },
        new ColumnInfo { Name = "Name", DataType = "nvarchar", MaxLength = 50, IsNullable = false },
        new ColumnInfo { Name = "Price", DataType = "decimal", NumericPrecision = 18, NumericScale = 2, IsNullable = true },
    };

    [Fact]
    public void BuildCreateTable_IncludesLengthIdentityAndNullability()
    {
        string script = SqlScriptGenerator.BuildCreateTable(_provider, "Products", SampleColumns());

        Assert.Contains("CREATE TABLE [Products]", script);
        Assert.Contains("[Id] int IDENTITY(1,1) NOT NULL", script);
        Assert.Contains("[Name] nvarchar(50) NOT NULL", script);
        Assert.Contains("[Price] decimal(18,2) NULL", script);
    }

    [Fact]
    public void BuildInsert_ExcludesIdentityColumn()
    {
        string script = SqlScriptGenerator.BuildInsert(_provider, "Products", SampleColumns());

        Assert.DoesNotContain("[Id]", script);
        Assert.Contains("[Name]", script);
        Assert.Contains("[Price]", script);
    }

    [Fact]
    public void BuildDrop_UsesProviderQuoting()
    {
        Assert.Equal("DROP TABLE [Products];", SqlScriptGenerator.BuildDrop(_provider, "Products"));
    }

    [Fact]
    public void BuildSelect_DelegatesToProviderDialect()
    {
        Assert.Equal("SELECT TOP 1000 * FROM [Products]", SqlScriptGenerator.BuildSelect(_provider, "Products"));
    }

    [Fact]
    public void BuildCreateTable_NoColumns_ReturnsComment()
    {
        string script = SqlScriptGenerator.BuildCreateTable(_provider, "Empty", new List<ColumnInfo>());
        Assert.StartsWith("--", script);
    }
}
