using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.NodeGeneration;

namespace RimXmlEdit.Core.Utils;

public class ExampleXmlManager
{
    private static readonly XNamespace MetaNs = "RimXmlEdit.Template";
    private readonly ILogger _log;

    private readonly Dictionary<string, TemplateData> _templates = new();

    private bool _isInitialized;

    public ExampleXmlManager()
    {
        _log = this.Log();
    }

    /// <summary>
    ///     初始化管理器，扫描指定目录下的模板文件
    /// </summary>
    /// <param name="templatesFolderPath">模板文件夹路径</param>
    /// <returns>是否初始化成功</returns>
    public bool Init(string? templatesFolderPath = null)
    {
        templatesFolderPath ??= TempConfig.TemplatesPath;

        if (_isInitialized) return true;
        _templates.Clear();

        if (!Directory.Exists(templatesFolderPath))
            return false;
        try
        {
            var files = Directory.GetFiles(templatesFolderPath, "*.xml", SearchOption.AllDirectories);
            foreach (var file in files) ParseTemplateFile(file);
            _isInitialized = true;
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError("Error init templates: {Error}", ex.Message);
            return false;
        }
    }

    /// <summary>
    ///     获取所有可用模板的列表 (key: 名称; value: 描述)
    /// </summary>
    public Dictionary<string, string> GetExampleXmlList()
    {
        return _templates.ToDictionary(k => k.Key, v => v.Value.Description);
    }

    public string GetDescription(string templateName)
    {
        return _templates.TryGetValue(templateName, out var data) ? data.Description : string.Empty;
    }

    /// <summary>
    ///     获取指定模板支持的过滤条件列表
    /// </summary>
    /// <param name="templateName">模板名称</param>
    public string[] GetFilter(string templateName)
    {
        return _templates.TryGetValue(templateName, out var data)
            ? data.FilterMap.Keys.ToArray()
            : Array.Empty<string>();
    }

    public List<FilterInfo> GetFilterInfos(string templateName)
    {
        return _templates.TryGetValue(templateName, out var data)
            ? data.FilterMap.Values.ToList()
            : new List<FilterInfo>();
    }

    public string GetTemplateType(string templateName)
    {
        return _templates.TryGetValue(templateName, out var data) ? data.DefType : string.Empty;
    }

    public IEnumerable<string> GetAllTemplateType()
    {
        return _templates.Select(t => t.Value.DefType).Distinct();
    }

    public IEnumerable<string> GetTemplateByType(string templateType)
    {
        return _templates.Where(t => t.Value.DefType == templateType).Select(t => t.Value.Name);
    }

    /// <summary>
    ///     根据模板名和过滤器，生成 <see cref="NodeBlueprint" />
    /// </summary>
    /// <param name="templateName">选择的模板名称</param>
    /// <param name="activeFilters">使用的过滤器, 黑名单</param>
    /// <returns></returns>
    public NodeBlueprint? CreateBlueprint(string templateName, string[]? activeFilters = null)
    {
        if (!_templates.TryGetValue(templateName, out var data))
            return null;
        var instance = new XElement(data.XmlContent);
        var filters = activeFilters != null
            ? new HashSet<string>(activeFilters)
            : new HashSet<string>();
        ProcessFiltersRecursively(instance, filters);
        return ConvertToBlueprint(instance);
    }


    private void ParseTemplateFile(string filePath)
    {
        try
        {
            var doc = XDocument.Load(filePath, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
            var rootDefType = doc.Root?.Attribute(MetaNs + "defType")?.Value ?? string.Empty;
            var globalFilters = new Dictionary<string, FilterInfo>();
            var filterDefsNode = doc.Root?.Element(MetaNs + "FilterDefs");

            if (filterDefsNode != null)
                foreach (var filterNode in filterDefsNode.Elements(MetaNs + "Filter"))
                {
                    var name = filterNode.Attribute("Name")?.Value;
                    if (string.IsNullOrEmpty(name)) continue;

                    globalFilters[name] = new FilterInfo
                    {
                        Name = name,
                        Description = filterNode.Attribute("Desc")?.Value ?? string.Empty,
                        Group = filterNode.Attribute("Group")?.Value ?? string.Empty // 读取Group
                    };
                }

            var templateNodes = doc.Descendants().Where(x => x.Attribute(MetaNs + "Name") != null);

            foreach (var node in templateNodes)
            {
                var name = node.Attribute(MetaNs + "Name")?.Value;
                if (string.IsNullOrEmpty(name)) continue;

                var usedFilters = new HashSet<string>();
                foreach (var el in node.DescendantsAndSelf())
                {
                    if (el.Attribute(MetaNs + "If")?.Value is { } s1) usedFilters.Add(s1);
                    if (el.Attribute(MetaNs + "Unless")?.Value is { } s2) usedFilters.Add(s2);

                    if (el.Name == MetaNs + "If" || el.Name == MetaNs + "Unless")
                        if (el.Attribute(MetaNs + "Cond")?.Value is { } s3)
                            usedFilters.Add(s3);
                }

                var filterMap = new Dictionary<string, FilterInfo>();
                foreach (var filterKey in usedFilters)
                    if (globalFilters.TryGetValue(filterKey, out var info))
                        filterMap[filterKey] = info;
                    else
                        filterMap[filterKey] = new FilterInfo { Name = filterKey };

                _templates[name] = new TemplateData
                {
                    Name = name,
                    Description = node.Attribute(MetaNs + "Desc")?.Value ?? name,
                    DefType = rootDefType,
                    XmlContent = node,
                    FilterMap = filterMap
                };
            }
        }
        catch (Exception ex)
        {
            _log.LogError("Error parsing template: {Path}, Message: {Error}", filePath, ex.Message);
        }
    }

    /// <summary>
    ///     递归处理 XML 节点，处理属性过滤和内联标签过滤
    /// </summary>
    private void ProcessFiltersRecursively(XElement element, HashSet<string> filters)
    {
        var nodes = element.Nodes().ToList();

        foreach (var node in nodes)
        {
            if (node is not XElement childEl) continue;

            if (childEl.Name.Namespace == MetaNs &&
                (childEl.Name.LocalName == "If" || childEl.Name.LocalName == "Unless"))
            {
                var cond = childEl.Attribute(MetaNs + "Cond")?.Value;
                var isIf = childEl.Name.LocalName == "If";
                var keep = true;

                if (!string.IsNullOrEmpty(cond))
                {
                    var contains = filters.Contains(cond);
                    keep = isIf ? contains : !contains;
                }

                if (keep)
                {
                    ProcessFiltersRecursively(childEl, filters);
                    childEl.ReplaceWith(childEl.Nodes());
                }
                else
                {
                    childEl.Remove();
                }
            }
            else
            {
                var remove = false;

                if (childEl.Attribute(MetaNs + "If")?.Value is { } ifVal)
                    if (ifVal.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .All(x => !filters.Contains(x)))
                        remove = true;

                if (!remove && childEl.Attribute(MetaNs + "Unless")?.Value is { } unlessVal)
                    if (unlessVal.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .All(x => filters.Contains(x)))
                        remove = true;

                if (remove)
                    childEl.Remove();
                else
                    ProcessFiltersRecursively(childEl, filters);
            }
        }
    }

    private NodeBlueprint ConvertToBlueprint(XElement element)
    {
        var bp = new NodeBlueprint(element.Name.LocalName);
        if (!element.HasElements) bp.Value = element.Value.Replace(" ", "").Replace("\n", "");
        foreach (var attr in element.Attributes())
        {
            if (attr.Name.Namespace == MetaNs) continue;
            if (attr.IsNamespaceDeclaration) continue;

            bp.AddAttribute(attr.Name.LocalName, attr.Value);
        }

        foreach (var child in element.Elements()) bp.AddChild(ConvertToBlueprint(child));

        return bp;
    }

    private class TemplateData
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string DefType { get; set; } = string.Empty;
        public XElement XmlContent { get; set; }
        public Dictionary<string, FilterInfo> FilterMap { get; set; } = new();
    }
}

public class FilterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
}