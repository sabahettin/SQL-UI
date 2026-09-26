namespace SqlUI.Core.Models;

public class ColumnInfo
{
    public string Name { get; set; } = "";
    public string? TableName { get; set; } // sadece IDbProvider.GetAllColumnsAsync sonucunda dolu
    public string DataType { get; set; } = "";
    public bool IsNullable { get; set; }
    public bool IsPrimaryKey { get; set; }
    public bool IsIdentity { get; set; }
    public int? MaxLength { get; set; }
    public int? NumericPrecision { get; set; }
    public int? NumericScale { get; set; }
}
