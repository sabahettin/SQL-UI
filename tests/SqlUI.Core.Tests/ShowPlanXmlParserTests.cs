using SqlUI.Core.ExecutionPlan;
using Xunit;

namespace SqlUI.Core.Tests;

public class ShowPlanXmlParserTests
{
    private const string SampleXml = """
        <?xml version="1.0" encoding="utf-16"?>
        <ShowPlanXML xmlns="http://schemas.microsoft.com/sqlserver/2004/07/showplan" Version="1.5">
          <BatchSequence>
            <Batch>
              <Statements>
                <StmtSimple StatementText="SELECT * FROM Customers c JOIN Orders o ON c.Id = o.CustomerId">
                  <QueryPlan>
                    <RelOp PhysicalOp="Nested Loops" LogicalOp="Inner Join" EstimatedTotalSubtreeCost="0.123" EstimateRows="10">
                      <NestedLoops>
                        <RelOp PhysicalOp="Clustered Index Scan" LogicalOp="Scan" EstimatedTotalSubtreeCost="0.05" EstimateRows="100" />
                        <RelOp PhysicalOp="Index Seek" LogicalOp="Seek" EstimatedTotalSubtreeCost="0.02" EstimateRows="1" />
                      </NestedLoops>
                    </RelOp>
                  </QueryPlan>
                </StmtSimple>
              </Statements>
            </Batch>
          </BatchSequence>
        </ShowPlanXML>
        """;

    [Fact]
    public void Parse_SingleStatement_ReturnsOneRoot()
    {
        var roots = ShowPlanXmlParser.Parse(SampleXml);

        var root = Assert.Single(roots);
        Assert.Contains("JOIN Orders", root.Label);
    }

    [Fact]
    public void Parse_NestedRelOps_BuildsCorrectTreeShape()
    {
        var roots = ShowPlanXmlParser.Parse(SampleXml);

        var topOp = Assert.Single(roots[0].Children);
        Assert.Contains("Nested Loops", topOp.Label);
        Assert.Equal(2, topOp.Children.Count);
        Assert.Contains("Clustered Index Scan", topOp.Children[0].Label);
        Assert.Contains("Index Seek", topOp.Children[1].Label);
    }

    [Fact]
    public void Parse_IncludesCostAndRowEstimates()
    {
        var roots = ShowPlanXmlParser.Parse(SampleXml);
        var topOp = roots[0].Children[0];

        Assert.Contains("0.123", topOp.Label);
        Assert.Contains("10", topOp.Label);
    }

    [Fact]
    public void Parse_NullOrEmpty_ReturnsEmptyList()
    {
        Assert.Empty(ShowPlanXmlParser.Parse(null));
        Assert.Empty(ShowPlanXmlParser.Parse(""));
    }
}
