namespace RimXmlEdit.Core.NodeGeneration
{
    internal class DefaultNodeRule : INodeGenerationRule, INodeUpdateRule
    {
        public bool CanApply(bool isPatch, string parentTagName, string selectedItem, string identification)
            => true;

        public NodeBlueprint CreateBlueprint(string selectedItem)
            => new NodeBlueprint(selectedItem);

        public NodeBlueprint UpdateNode(NodeBlueprint defaultRootNode, string parentTgName, string tgName, string? value)
            => NodeBlueprint.None;
    }
}
