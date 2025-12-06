namespace RimXmlEdit.Core.NodeGeneration;

// 可以支持模版文件, 快速导入指定需求的节点序列, 目前还未实现
public class NodeGenerationService
{
    private readonly List<INodeGenerationRule> _rules;
    private readonly List<INodeUpdateRule> _updateRules;

    public NodeGenerationService(FileFromRule fileFromRule)
    {
        var rule0 = new CompsListRule();
        var rule1 = new ThingDefTemplateRule();
        var rule2 = new DefaultNodeRule();
        var rule3 = new PatchesListRule();
        var rule4 = fileFromRule;
        _rules = new List<INodeGenerationRule>
        {
            rule0, rule1, rule4
        };

        _updateRules = new List<INodeUpdateRule>
        {
            rule3
        };
        
        _rules.Add(rule2);
        _updateRules.Add(rule2);
    }

    public NodeBlueprint Generate(bool isPatch, string parentTagName, string selectedItem, string identification)
    {
        var rule = _rules.FirstOrDefault(r => r.CanApply(isPatch, parentTagName, selectedItem, identification));
        return rule == null
            ? throw new InvalidOperationException("No matching rule found for node generation.")
            : rule.CreateBlueprint(selectedItem);
    }

    public NodeBlueprint RefreshNodeStructure(bool isPatch, string parentTgName, string tgName, string? value)
    {
        var defaultNode = new NodeBlueprint("defaultRoot", null);
        var rule = _updateRules.FirstOrDefault(r => r.CanApply(isPatch, parentTgName, tgName, value));
        return rule == null
            ? throw new InvalidOperationException("No matching rule found for refresh node struct.")
            : rule.UpdateNode(defaultNode, parentTgName, tgName, value);
    }
}
