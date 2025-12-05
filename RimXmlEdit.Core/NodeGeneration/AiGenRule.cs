namespace RimXmlEdit.Core.NodeGeneration;

public class AiGenRule : INodeGenerationRule, INodeUpdateRule
{
    public bool CanApply(bool isPatch, string parentTagName, string selectedItem, string identification)
    {
        throw new NotImplementedException();
    }

    public NodeBlueprint CreateBlueprint(string selectedItem)
    {
        throw new NotImplementedException();
    }

    public NodeBlueprint UpdateNode(NodeBlueprint defaultRootNode, string parentTgName, string tgName, string? value)
    {
        throw new NotImplementedException();
    }
}