using RimXmlEdit.Core.Utils;

namespace RimXmlEdit.Core.NodeGeneration;

public class FileFromRule : INodeGenerationRule, INodeUpdateRule
{
    private readonly ExampleXmlManager _exampleXmlManager;
    private string[] _cachedFilters = Array.Empty<string>();

    public FileFromRule(ExampleXmlManager exampleXmlManager)
    {
        _exampleXmlManager = exampleXmlManager;
    }

    public bool CanApply(bool isPatch, string parentTagName, string selectedItem, string identification)
    {
        if (!string.IsNullOrEmpty(identification))
            _cachedFilters = identification.Split(';',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        else
            _cachedFilters = Array.Empty<string>();

        var templates = _exampleXmlManager.GetExampleXmlList();
        return templates.ContainsKey(selectedItem);
    }

    public NodeBlueprint CreateBlueprint(string selectedItem)
    {
        var bp = _exampleXmlManager.CreateBlueprint(selectedItem, _cachedFilters.Skip(1).ToArray());
        return bp ?? NodeBlueprint.None;
    }

    // 样板不涉及更新操作
    public NodeBlueprint UpdateNode(NodeBlueprint defaultRootNode, string parentTgName, string tgName, string? value)
    {
        return defaultRootNode;
    }
}