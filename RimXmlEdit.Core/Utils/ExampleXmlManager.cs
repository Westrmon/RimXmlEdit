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

    public Dictionary<string, List<string>> _defTypeToName = new();
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
        if (_templates.TryGetValue(templateName, out var data))
            return data.FilterMap.Select(kvp => new FilterInfo
            {
                Name = kvp.Key,
                Description = kvp.Value
            }).ToList();
        return new List<FilterInfo>();
    }

    public string GetTemplateType(string templateName)
    {
        return _templates.TryGetValue(templateName, out var data) ? data.DefType : string.Empty;
    }

    public IEnumerable<string> GetAllTemplateType()
    {
        return _defTypeToName.Keys;
    }

    public IEnumerable<string> GetTemplateByType(string templateType)
    {
        return _defTypeToName.TryGetValue(templateType, out var data) ? data : Array.Empty<string>();
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
            var doc = XDocument.Load(filePath, LoadOptions.SetLineInfo);
            var rootDefType = doc.Root?.Attribute(MetaNs + "defType")?.Value ?? string.Empty;
            var globalDescriptions = new Dictionary<string, string>();
            var filterDefsNode = doc.Root?.Element(MetaNs + "FilterDefs");
            if (filterDefsNode != null)
                foreach (var filterNode in filterDefsNode.Elements(MetaNs + "Filter"))
                {
                    var name = filterNode.Attribute("Name")?.Value;
                    var desc = filterNode.Attribute("Desc")?.Value;
                    if (!string.IsNullOrEmpty(name)) globalDescriptions[name] = desc ?? string.Empty;
                }

            var templateNodes = doc.Descendants().Where(x => x.Attribute(MetaNs + "Name") != null);

            foreach (var node in templateNodes)
            {
                var name = node.Attribute(MetaNs + "Name")?.Value;
                if (string.IsNullOrEmpty(name)) continue;

                var templateDesc = node.Attribute(MetaNs + "Desc")?.Value ?? name;
                // var currentDefType = node.Attribute(MetaNs + "defType")?.Value ?? rootDefType;
                var usedFilters = new HashSet<string>();
                foreach (var el in node.DescendantsAndSelf())
                {
                    if (el.Attribute(MetaNs + "If")?.Value is { } s1)
                    {
                        foreach (var item in s1.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            usedFilters.Add(item);
                        }
                        
                    }
                    if (el.Attribute(MetaNs + "Unless")?.Value is { } s2)
                    {
                        foreach (var item in s2.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            usedFilters.Add(item);
                        }
                    }
                }

                var filterMap = new Dictionary<string, string>();
                foreach (var filterKey in usedFilters)
                    if (globalDescriptions.TryGetValue(filterKey, out var desc)) filterMap[filterKey] = desc;
                    else filterMap[filterKey] = string.Empty;

                _templates[name] = new TemplateData
                {
                    Name = name,
                    Description = templateDesc,
                    DefType = rootDefType,
                    XmlContent = node,
                    FilterMap = filterMap
                };
                _defTypeToName.TryAdd(rootDefType, new List<string>());
                _defTypeToName[rootDefType].Add(name);
            }
        }
        catch (Exception ex)
        {
            _log.LogError("Error parsing template: {Path}, Message: {Error}", filePath, ex.Message);
        }
    }

    private void ProcessFiltersRecursively(XElement element, HashSet<string> filters)
    {
        var children = element.Elements().ToList();
        foreach (var child in children)
        {
            var remove = false;

            if (child.Attribute(MetaNs + "If")?.Value is { } ifVal)
            {
                if (ifVal.Split('|',  StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .All(x => !filters.Contains(x))) 
                    remove = true;
            }

            if (!remove && child.Attribute(MetaNs + "Unless")?.Value is { } unlessVal)
            {
                if (unlessVal.Split('|',  StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .All(x => filters.Contains(x))) 
                    remove = true;
            }

            if (remove)
                child.Remove();
            else
                ProcessFiltersRecursively(child, filters);
        }
    }

    private NodeBlueprint ConvertToBlueprint(XElement element)
    {
        var bp = new NodeBlueprint(element.Name.LocalName);
        if (!element.HasElements) bp.Value = element.Value;
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
        public Dictionary<string, string> FilterMap { get; set; } = new();
    }
}

public class FilterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}