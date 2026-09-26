using System.Xml.Linq;

namespace SqlUI.Core.ExecutionPlan;

// SQL Server'in "SET SHOWPLAN_XML ON" ile dondurdugu XML'i basit bir agaca cevirir.
// Saf XML ayristirma - hicbir ADO.NET/baglanti bagimliligi yok, bu yuzden dogrudan test edilebilir.
public static class ShowPlanXmlParser
{
    private static readonly XNamespace Ns = "http://schemas.microsoft.com/sqlserver/2004/07/showplan";

    public static List<PlanNode> Parse(string? planXml)
    {
        var roots = new List<PlanNode>();
        if (string.IsNullOrWhiteSpace(planXml)) return roots;

        XDocument doc = XDocument.Parse(planXml);
        foreach (var stmt in doc.Descendants(Ns + "StmtSimple"))
        {
            string stmtText = stmt.Attribute("StatementText")?.Value ?? "Statement";
            var stmtNode = new PlanNode { Label = stmtText };

            var queryPlan = stmt.Element(Ns + "QueryPlan");
            if (queryPlan != null)
                foreach (var relOp in GetChildRelOps(queryPlan))
                    stmtNode.Children.Add(BuildNode(relOp));

            roots.Add(stmtNode);
        }
        return roots;
    }

    // RelOp elemanlari ic ice gecebilir (orn. NestedLoops icinde iki RelOp); dogrudan alt
    // RelOp'lari bulmak icin baska bir RelOp'a rastlayana kadar icine iniyoruz, onu asmiyoruz -
    // cunku o alt agac kendi BuildNode cagrisinda ayrica islenecek.
    private static IEnumerable<XElement> GetChildRelOps(XElement element)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name.LocalName == "RelOp") yield return child;
            else foreach (var nested in GetChildRelOps(child)) yield return nested;
        }
    }

    private static PlanNode BuildNode(XElement relOp)
    {
        string physicalOp = relOp.Attribute("PhysicalOp")?.Value ?? "?";
        string? cost = relOp.Attribute("EstimatedTotalSubtreeCost")?.Value;
        string? rows = relOp.Attribute("EstimateRows")?.Value;
        var node = new PlanNode { Label = $"{physicalOp}   (Tahmini Maliyet: {cost}, Tahmini Satir: {rows})" };
        foreach (var child in GetChildRelOps(relOp)) node.Children.Add(BuildNode(child));
        return node;
    }
}
