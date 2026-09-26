using System.Data;

namespace SqlUI.Core.Models;

public class QueryExecutionResult
{
    public List<DataTable> ResultSets { get; } = new();
    public List<string> Messages { get; } = new();
    public TimeSpan Elapsed { get; set; }
}
