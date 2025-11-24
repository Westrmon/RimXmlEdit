using RimXmlEdit.Core.Entries;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace RimXmlEdit.Core.XmlOperator;

internal static class RXmlMetadataParser
{
    private static readonly Regex PlaceholderRegex = new Regex(@"^{(.*)}$", RegexOptions.Compiled);

    /// <summary>
    /// 解析根节点，提取所有子节点的元数据。
    /// </summary>
    public static List<NodeMeta> Parse(XElement root)
    {
        var results = new List<NodeMeta>();
        Traverse(root, "", results);
        return results;
    }

    /// <summary>
    /// 递归遍历 XML 节点树。
    /// </summary>
    private static void Traverse(XElement element, string currentPath, List<NodeMeta> results)
    {
        string path = $"{currentPath}/{element.Name.LocalName}";

        // 为当前节点创建并填充元数据
        var meta = new NodeMeta
        {
            Path = path,
            Name = element.Name.LocalName,

            Description = GetAttributeValue(element, "rxe_dec"),
            Parent = GetAttributeValue(element, "rxe_parent"),
            Type = GetAttributeValue(element, "rxe_type"),
            Range = GetAttributeValue(element, "rxe_range"),
            Default = GetAttributeValue(element, "rxe_default"),
            IsList = GetBoolAttribute(element, "rxe_list"),
            Required = GetBoolAttribute(element, "rxe_required"),
            Editable = GetBoolAttribute(element, "rxe_editable", true), // 默认为 true
            Hidden = GetBoolAttribute(element, "rxe_hide"),
            IsFile = GetBoolAttribute(element, "rxe_isfile"),
            EnumValues = GetAttributeValue(element, "rxe_enum")?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray()
        };

        // 提取占位符
        string? textContent = element.Nodes().OfType<XText>().FirstOrDefault()?.Value.Trim();
        if (!string.IsNullOrEmpty(textContent))
        {
            var match = PlaceholderRegex.Match(textContent);
            if (match.Success)
            {
                meta.Placeholder = match.Groups[1].Value;
            }
        }

        results.Add(meta);

        // 递归遍历子节点
        foreach (var child in element.Elements())
        {
            Traverse(child, path, results);
        }
    }

    private static string? GetAttributeValue(XElement element, string attributeName)
    {
        return element.Attribute(attributeName)?.Value;
    }

    private static bool GetBoolAttribute(XElement element, string attributeName, bool defaultValue = false)
    {
        string? value = GetAttributeValue(element, attributeName);
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return bool.TryParse(value, out bool result) ? result : defaultValue;
    }
}
