namespace RimXmlEdit.Core.NodeGeneration;

internal class ThingDefTemplateRule : INodeGenerationRule
{
    public bool CanApply(bool isPatch, string parentTagName, string selectedItem, string identification)
        => selectedItem == "ThingDef";

    public NodeBlueprint CreateBlueprint(string selectedItem)
    {
        var root = new NodeBlueprint("ThingDef");
        root.AddChild(new NodeBlueprint("defName", "an unique name"));
        root.AddChild(new NodeBlueprint("label", "display name"));
        return root;
    }
}
