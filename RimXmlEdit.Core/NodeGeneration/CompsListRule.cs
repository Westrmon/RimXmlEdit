namespace RimXmlEdit.Core.NodeGeneration;

internal class CompsListRule : INodeGenerationRule
{
    public bool CanApply(bool isPatch, string parentTagName, string selectedItem, string identification)
        => parentTagName.Equals("comps", StringComparison.OrdinalIgnoreCase);

    public NodeBlueprint CreateBlueprint(string selectedItem)
        => new NodeBlueprint("li").AddAttribute("Class", selectedItem);
}
