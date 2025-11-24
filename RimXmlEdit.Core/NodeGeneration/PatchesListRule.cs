namespace RimXmlEdit.Core.NodeGeneration;

// patches 模板需要的类型是Class, tagname固定
internal class PatchesListRule : INodeUpdateRule
{
    public bool CanApply(bool isPatch, string parentTagName, string tgName, string identification)
        => isPatch && tgName == "Operation" || tgName == "li" || tgName == "match";

    public NodeBlueprint UpdateNode(NodeBlueprint defaultRootNode, string parentTgName, string tgName, string? value)
    {
        if (value == "PatchOperationFindMod")
        {
            defaultRootNode.Children.Add(new NodeBlueprint
            {
                TagName = "mods",
                Children = new List<NodeBlueprint> { new NodeBlueprint
                {
                    TagName = "li",
                } }
            });
            defaultRootNode.Children.Add(new NodeBlueprint
            {
                TagName = "match"
            });
        }
        else if (value == "PatchOperationAdd" || value == "PatchOperationReplace")
        {
            defaultRootNode.AddChild(new NodeBlueprint
            {
                TagName = "xpath"
            });
            defaultRootNode.AddChild(new NodeBlueprint
            {
                TagName = "value"
            });
        }
        else if (value == "PatchOperationSequence")
        {
            defaultRootNode.AddChild(new NodeBlueprint
            {
                TagName = "success"
            });
            defaultRootNode.AddChild(new NodeBlueprint
            {
                TagName = "operations",
                Children = new List<NodeBlueprint>
                {
                    new NodeBlueprint
                    {
                        TagName = "li"
                    }
                }
            });
        }

        return defaultRootNode;
    }
}
