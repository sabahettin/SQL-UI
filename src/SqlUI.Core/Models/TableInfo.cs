namespace SqlUI.Core.Models;

public class TableInfo
{
    public string Name { get; set; } = "";
    public string? Schema { get; set; }
    public bool IsView { get; set; }

    public string QualifiedName => Schema is null ? Name : $"{Schema}.{Name}";
}
