namespace SqlUI.Core.IntelliSense;

public class TableColumnSet
{
    public string TableName { get; set; } = "";
    public List<string> Columns { get; } = new();
}
