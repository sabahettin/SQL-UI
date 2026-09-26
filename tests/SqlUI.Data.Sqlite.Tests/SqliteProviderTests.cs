using SqlUI.Core.Models;
using SqlUI.Data.Sqlite;
using Xunit;

namespace SqlUI.Data.Sqlite.Tests;

// IDbProvider soyutlamasinin gercekten calistigini bir sunucuya ihtiyac duymadan dogrular:
// SQLite dosya tabanli oldugu icin CI'da veya bu makinede hicbir kurulum gerekmeden kosar.
public class SqliteProviderTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteProvider _provider = new();
    private readonly string _connectionString;

    public SqliteProviderTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"sqlui_test_{Guid.NewGuid():N}.db");
        _connectionString = _provider.BuildConnectionString(new ConnectionProfile { Server = _dbPath }, null);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task ExecuteQueryAsync_CreateInsertSelect_ReturnsExpectedRows()
    {
        await _provider.ExecuteQueryAsync(_connectionString, null,
            "CREATE TABLE Customers (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL)");
        await _provider.ExecuteQueryAsync(_connectionString, null,
            "INSERT INTO Customers (Name) VALUES ('Ada'); INSERT INTO Customers (Name) VALUES ('Grace')");

        var result = await _provider.ExecuteQueryAsync(_connectionString, null, "SELECT Id, Name FROM Customers ORDER BY Id");

        Assert.Single(result.ResultSets);
        Assert.Equal(2, result.ResultSets[0].Rows.Count);
        Assert.Equal("Ada", result.ResultSets[0].Rows[0]["Name"]);
    }

    [Fact]
    public async Task GetTablesAsync_AfterCreateTable_ListsIt()
    {
        await _provider.ExecuteQueryAsync(_connectionString, null, "CREATE TABLE Orders (Id INTEGER PRIMARY KEY)");

        var tables = await _provider.GetTablesAsync(_connectionString, null);

        Assert.Contains(tables, t => t.Name == "Orders" && !t.IsView);
    }

    [Fact]
    public async Task GetColumnsAsync_ReturnsPrimaryKeyFlag()
    {
        await _provider.ExecuteQueryAsync(_connectionString, null,
            "CREATE TABLE Products (Id INTEGER PRIMARY KEY, Price REAL NOT NULL)");

        var columns = await _provider.GetColumnsAsync(_connectionString, null, "Products");

        var idColumn = Assert.Single(columns, c => c.Name == "Id");
        Assert.True(idColumn.IsPrimaryKey);
        var priceColumn = Assert.Single(columns, c => c.Name == "Price");
        Assert.False(priceColumn.IsNullable);
    }
}
