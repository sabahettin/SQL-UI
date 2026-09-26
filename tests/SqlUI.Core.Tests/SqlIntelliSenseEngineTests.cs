using SqlUI.Core.IntelliSense;
using Xunit;

namespace SqlUI.Core.Tests;

public class SqlIntelliSenseEngineTests
{
    private readonly SqlIntelliSenseEngine _engine = new();

    private static List<TableColumnSet> SampleMetadata()
    {
        var customers = new TableColumnSet { TableName = "Customers" };
        customers.Columns.AddRange(new[] { "Id", "Name", "Email" });
        var orders = new TableColumnSet { TableName = "Orders" };
        orders.Columns.AddRange(new[] { "Id", "CustomerId", "OrderDate" });
        return new List<TableColumnSet> { customers, orders };
    }

    [Fact]
    public void GetSuggestions_AfterFrom_SuggestsOnlyTables()
    {
        var result = _engine.GetSuggestions("Cust", "SELECT * FROM ", SampleMetadata());

        Assert.Contains("Customers", result);
        Assert.DoesNotContain("CustomerId", result); // Orders.CustomerId kolonu - FROM baglaminda gorunmemeli
    }

    [Fact]
    public void GetSuggestions_AfterJoin_SuggestsOnlyTables()
    {
        var result = _engine.GetSuggestions("Ord", "SELECT * FROM Customers c JOIN ", SampleMetadata());

        Assert.Equal(new[] { "Orders" }, result);
    }

    [Fact]
    public void GetSuggestions_AfterSelect_SuggestsOnlyColumns()
    {
        var result = _engine.GetSuggestions("Id", "SELECT ", SampleMetadata());

        Assert.Contains("Id", result);
        Assert.DoesNotContain("Customers", result);
        Assert.DoesNotContain("Orders", result);
    }

    [Fact]
    public void GetSuggestions_AfterWhere_SuggestsOnlyColumns()
    {
        var result = _engine.GetSuggestions("Cust", "SELECT * FROM Orders WHERE ", SampleMetadata());

        Assert.Equal(new[] { "CustomerId" }, result);
    }

    [Fact]
    public void GetSuggestions_NoContextKeyword_SuggestsTablesAndColumns()
    {
        // Sorgunun en basinda henuz hicbir anahtar kelime yok - hem tablo hem kolon onerilir.
        var result = _engine.GetSuggestions("Cust", "", SampleMetadata());

        Assert.Contains("Customers", result);
    }

    [Fact]
    public void GetSuggestions_TableDotPartialColumn_SuggestsOnlyThatTablesColumns()
    {
        var result = _engine.GetSuggestions("Customers.Na", "SELECT ", SampleMetadata());

        Assert.Equal(new[] { "Name" }, result);
    }

    [Fact]
    public void GetSuggestions_UnknownQualifier_FallsBackToAllColumns()
    {
        // "c." bir alias olabilir, hangi tabloya ait oldugunu bilemeyiz - tum kolonlarda ara.
        var result = _engine.GetSuggestions("c.Id", "SELECT ", SampleMetadata());

        Assert.Contains("Id", result);
    }

    [Fact]
    public void GetSuggestions_EmptyWord_ReturnsEmpty()
    {
        Assert.Empty(_engine.GetSuggestions("", "SELECT ", SampleMetadata()));
    }

    [Theory]
    [InlineData("SELECT * FROM Cust", "Cust")]
    [InlineData("SELECT c.Na", "c.Na")]
    [InlineData("SELECT Id, Name FROM Orders WHERE ", "")]
    [InlineData("SELECT TOP 10 * FROM (Customers", "Customers")]
    public void GetWordBeforeCursor_StopsAtWhitespaceAndBrackets(string text, string expected)
    {
        Assert.Equal(expected, SqlIntelliSenseEngine.GetWordBeforeCursor(text, text.Length));
    }
}
