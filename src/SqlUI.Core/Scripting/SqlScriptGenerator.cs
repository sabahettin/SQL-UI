using SqlUI.Core.Models;

namespace SqlUI.Core.Scripting;

// Motor-bagimsiz script uretimi. ColumnInfo.DataType zaten saglayicinin kendi rapor ettigi
// gercek tur adidir (SQL Server: "int"/"nvarchar"; SQLite: "INTEGER"/"TEXT"...), bu yuzden
// buradaki kod hicbir motora ozel tur haritalamasi yapmadan dogru dialect'i uretir.
public static class SqlScriptGenerator
{
    public static string BuildCreateTable(IDbProvider provider, string table, IReadOnlyList<ColumnInfo> columns)
    {
        if (columns.Count == 0) return $"-- '{table}' icin kolon bilgisi bulunamadi.";

        var lines = columns.Select(c =>
        {
            string type = c.DataType;
            if (c.MaxLength is > 0) type += $"({c.MaxLength})";
            else if (c.NumericPrecision is > 0) type += c.NumericScale is > 0 ? $"({c.NumericPrecision},{c.NumericScale})" : $"({c.NumericPrecision})";

            string identity = c.IsIdentity ? " IDENTITY(1,1)" : "";
            string nullability = c.IsNullable ? "NULL" : "NOT NULL";
            return $"    {provider.QuoteIdentifier(c.Name)} {type}{identity} {nullability}";
        });

        return $"CREATE TABLE {provider.QuoteIdentifier(table)} (\n{string.Join(",\n", lines)}\n);";
    }

    public static string BuildDrop(IDbProvider provider, string table)
        => $"DROP TABLE {provider.QuoteIdentifier(table)};";

    public static string BuildSelect(IDbProvider provider, string table)
        => provider.BuildSelectTopQuery(table, 1000);

    public static string BuildInsert(IDbProvider provider, string table, IReadOnlyList<ColumnInfo> columns)
    {
        var insertable = columns.Where(c => !c.IsIdentity).ToList();
        if (insertable.Count == 0) return $"-- '{table}' icin duzenlenebilir kolon bulunamadi.";

        string columnList = string.Join(", ", insertable.Select(c => provider.QuoteIdentifier(c.Name)));
        string valueList = string.Join(", ", insertable.Select(_ => "<deger>"));
        return $"INSERT INTO {provider.QuoteIdentifier(table)} (\n    {columnList}\n)\nVALUES (\n    {valueList}\n);";
    }
}
