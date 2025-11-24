using RimXmlEdit.Core.Entries;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RimXmlEdit.Core.XmlOperator;

public static class RXmlWriter
{
    private static readonly Regex PlaceholderRegex = new Regex(@"^{(.*)}$", RegexOptions.Compiled);

    private static readonly Dictionary<string, RXmlTemplate> ParentTemplate = new Dictionary<string, RXmlTemplate>();

    /// <summary>
    /// 1. 移除所有的 rxe_* 属性。
    /// 2. 将所有 {placeholder} 替换为提供的实际值。
    /// </summary>
    /// <param name="mergedTemplateRoot"> 经过继承合并后的模板根节点。 </param>
    /// <param name="placeholderValues"> 一个字典，键是占位符名称（不含花括号），值是用户提供的替换内容。 </param>
    /// <returns> 一个全新的、干净的 XElement，可直接保存为文件。 </returns>
    public static XElement Finalize(
        XElement mergedTemplateRoot,
        Dictionary<string, object> placeholderValues,
        TemplateManager templateManager)
    {
        ArgumentNullException.ThrowIfNull(mergedTemplateRoot);
        placeholderValues ??= new Dictionary<string, object>();

        // 创建一个深拷贝，以避免修改原始的、已解析的模板对象
        XComment comment = new XComment("此文件由 RimXmlEdit 自动生成");
        XElement finalXml = new XElement(mergedTemplateRoot);

        // 递归处理所有节点
        ProcessNodeAndCleanup(finalXml, placeholderValues, templateManager);
        // 移除所有没有内容的节点
        finalXml.AddFirst(comment);
        return finalXml;
    }

    // <summary>
    /// 以混合顺序（前序展开、后序清理）递归处理并清理节点。 </summary>
    private static void ProcessNodeAndCleanup(
        XElement element,
        IReadOnlyDictionary<string, object> placeholderValues,
        TemplateManager templateManager)
    {
        bool isListContainer = IsAttributeTrue(element, "rxe_list");
        bool removeIfEmpty = !IsAttributeTrue(element, "rxe_required");
        // 如果当前节点是列表容器，则在这里处理其子项的生成。
        if (isListContainer)
        {
            var templateItem = element.Elements().FirstOrDefault(); // 获取作为模板的子项，如 <li>
            if (templateItem != null)
            {
                if (placeholderValues.TryGetValue(element.Name.LocalName, out object? listData)
                    && listData is List<Dictionary<string, string>> complexListData)
                {
                    string? parentName = templateItem.Attribute("rxe_parent")?.Value;
                    if (!string.IsNullOrEmpty(parentName))
                    {
                        // 移除原始模板项
                        templateItem.Remove();

                        foreach (var itemData in complexListData)
                        {
                            if (!ParentTemplate.TryGetValue(parentName, out var parentTemplateCached))
                            {
                                parentTemplateCached = templateManager.ParseTemplate(parentName);
                                ParentTemplate.Add(parentName, parentTemplateCached);
                            }
                            var newItem = new XElement(parentTemplateCached.Root);

                            // 用当前项的数据填充克隆的模板
                            PopulateNode(newItem, itemData);

                            element.Add(newItem);
                        }
                    }
                }
                else
                {
                    // 查找子项中的占位符
                    var textNode = templateItem.Nodes().OfType<XText>().FirstOrDefault();
                    var match = textNode != null ? PlaceholderRegex.Match(textNode.Value.Trim()) : null;

                    if (match != null && match.Success)
                    {
                        string placeholderName = match.Groups[1].Value;
                        if (placeholderValues.TryGetValue(placeholderName, out var value) && value is string stringValue)
                        {
                            // 按逗号分割值，并移除空项
                            var values = stringValue.Split(',')
                                                    .Select(s => s.Trim())
                                                    .Where(s => !string.IsNullOrEmpty(s))
                                                    .ToList();

                            if (values.Count != 0)
                            {
                                // 先移除原始的模板项
                                templateItem.Remove();

                                // 为每个值创建一个克隆并填充
                                foreach (var singleValue in values)
                                {
                                    var newItem = new XElement(templateItem);
                                    newItem.SetValue(singleValue);
                                    element.Add(newItem);
                                }
                            }
                        }
                    }
                }
            }
        }

        // 使用 .ToList() 是因为列表展开可能会修改子节点集合
        foreach (var child in element.Elements().ToList())
        {
            ProcessNodeAndCleanup(child, placeholderValues, templateManager);
        }

        // 仅当此节点不是列表容器时才执行简单替换，因为列表容器的占位符已在步骤2处理。
        if (!isListContainer)
        {
            var textNode = element.Nodes().OfType<XText>().FirstOrDefault();
            if (textNode != null)
            {
                var match = PlaceholderRegex.Match(textNode.Value.Trim());
                if (match != null && match.Success)
                {
                    string placeholderName = match.Groups[1].Value;
                    if (placeholderValues.TryGetValue(placeholderName, out object? finalValue)
                        && finalValue is string stringValue)
                    {
                        element.SetValue(stringValue);
                    }
                    else
                    {
                        element.SetValue(string.Empty);
                    }
                }
            }
        }

        element.Attributes()
               .Where(attr => attr.Name.LocalName.StartsWith("rxe_"))
               .Remove();

        element.Elements("li")
               .Where(li => !li.HasElements && !li.Attributes().Any() && string.IsNullOrWhiteSpace(li.Value))
               .Remove();

        element.Nodes().OfType<XComment>()
                       .ToList()
                       .ForEach(c => c.Remove());

        element.Element("RXEName")?.Remove();

        if (isListContainer && !element.HasElements)
        {
            element.Remove();
            return;
        }

        if (removeIfEmpty && !element.HasElements
            && !element.Attributes().Any()
            && string.IsNullOrWhiteSpace(element.Value))
        {
            element.Remove();
            return;
        }
    }

    private static void PopulateNode(XElement element, IReadOnlyDictionary<string, string> values)
    {
        var textNode = element.Nodes().OfType<XText>().FirstOrDefault();
        if (textNode != null)
        {
            var match = PlaceholderRegex.Match(textNode.Value.Trim());
            if (match != null && match.Success)
            {
                string placeholderName = match.Groups[1].Value;
                if (values.TryGetValue(placeholderName, out string? finalValue))
                {
                    element.SetValue(finalValue ?? "");
                }
            }
        }
        foreach (var child in element.Elements())
        {
            PopulateNode(child, values);
        }
    }

    private static bool IsAttributeTrue(XElement element, string attributeName)
    {
        return element.Attribute(attributeName)?.Value.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
    }
}
