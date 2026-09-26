namespace SqlUI.Core.ExecutionPlan;

public class PlanNode
{
    public string Label { get; set; } = "";
    public List<PlanNode> Children { get; } = new();
}
