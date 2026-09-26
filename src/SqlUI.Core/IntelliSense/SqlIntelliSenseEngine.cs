using System.Text.RegularExpressions;

namespace SqlUI.Core.IntelliSense;

// Motor-bagimsiz tamamlama mantigi: hangi tablo/kolonlarin onerilecegini, kullanicinin imlecten
// once yazdigi tek kelimeye (varsa "tablo." veya "alias." niteleyicisiyle) ve o kelimeden hemen
// once gelen SQL anahtar kelimesine (baglam) bakarak belirler. UI katmani (AvalonEdit) imlecten
// once yazilan kelimeyi, o kelimeden onceki metni ve tablo/kolon metadata'sini verir.
public class SqlIntelliSenseEngine
{
    // FROM/JOIN/INTO/UPDATE'den sonra sadece tablo adi beklenir; digerlerinden (SELECT, WHERE, ON,
    // AND, OR, SET, BY, HAVING, VALUES) sonra kolon adi beklenir. En yakin (kelimeye en yakin) anahtar
    // kelime hangisiyse baglam odur - RightToLeft ile ilk eslesme metnin sonuna en yakin olandir.
    private static readonly Regex ContextKeywordRegex = new(
        @"\b(SELECT|FROM|WHERE|JOIN|INTO|UPDATE|SET|ON|AND|OR|BY|HAVING|VALUES)\b",
        RegexOptions.IgnoreCase | RegexOptions.RightToLeft | RegexOptions.Compiled);

    private static readonly HashSet<string> TableContextKeywords =
        new(StringComparer.OrdinalIgnoreCase) { "FROM", "JOIN", "INTO", "UPDATE" };

    public List<string> GetSuggestions(string wordBeforeCursor, string textBeforeWord, IReadOnlyList<TableColumnSet> metadata)
    {
        if (string.IsNullOrEmpty(wordBeforeCursor) || metadata == null) return new List<string>();

        int dotIndex = wordBeforeCursor.LastIndexOf('.');
        if (dotIndex >= 0)
        {
            string qualifier = wordBeforeCursor[..dotIndex].TrimStart('[').TrimEnd(']');
            string partialColumn = wordBeforeCursor[(dotIndex + 1)..];

            var table = metadata.FirstOrDefault(t => string.Equals(t.TableName, qualifier, StringComparison.OrdinalIgnoreCase));
            if (table != null)
            {
                return table.Columns
                    .Where(c => c.StartsWith(partialColumn, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c)
                    .ToList();
            }

            // Bilinmeyen niteleyici (muhtemelen alias) - tum kolonlar arasinda ara.
            return metadata.SelectMany(t => t.Columns).Distinct()
                .Where(c => c.StartsWith(partialColumn, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c)
                .ToList();
        }

        var tableMatches = metadata
            .Where(t => t.TableName.StartsWith(wordBeforeCursor, StringComparison.OrdinalIgnoreCase))
            .Select(t => t.TableName)
            .Distinct();

        var columnMatches = metadata
            .SelectMany(t => t.Columns)
            .Distinct()
            .Where(c => c.StartsWith(wordBeforeCursor, StringComparison.OrdinalIgnoreCase));

        var contextMatch = ContextKeywordRegex.Match(textBeforeWord ?? "");
        if (contextMatch.Success)
        {
            bool tableContext = TableContextKeywords.Contains(contextMatch.Value);
            return (tableContext ? tableMatches : columnMatches).OrderBy(x => x).ToList();
        }

        // Baglam belirlenemedi (orn. sorgunun en basi) - hem tablo hem kolon oner.
        return tableMatches.Concat(columnMatches).Distinct().OrderBy(x => x).ToList();
    }

    // Imlecten once yazilmakta olan kelimeyi (varsa "tablo." niteleyicisi dahil) bulur.
    // Bosluk, "(" ve "," sinirlarinda durur, "." icinde kalmaya devam eder.
    public static string GetWordBeforeCursor(string text, int cursorPos)
    {
        int start = cursorPos - 1;
        while (start >= 0 && !char.IsWhiteSpace(text[start]) && text[start] != '[' && text[start] != '(' && text[start] != ',')
            start--;
        return text[(start + 1)..cursorPos];
    }
}
