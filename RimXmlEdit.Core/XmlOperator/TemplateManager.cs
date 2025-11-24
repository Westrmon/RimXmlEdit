using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using System.Xml.Linq;

namespace RimXmlEdit.Core.XmlOperator;

public class TemplateManager
{
    private readonly ILogger _log;
    private readonly Dictionary<string, XElement> _templates = new(StringComparer.OrdinalIgnoreCase);

    public TemplateManager()
    {
        _log = this.Log();
    }

    /// <summary> 从模板文件夹加载所有 .xml 文件作为模板。 模板的名称由其 <RXEName> 节点内容决定。 </summary> <param
    /// name="folderPath">包含 XML 模板的文件夹路径。</param>
    public void LoadTemplates(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            _log.LogWarning("Directory not found: {}", folderPath);
            return;
        }

        foreach (var file in Directory.GetFiles(folderPath, "*.xml"))
        {
            try
            {
                XElement? root = XDocument.Load(file).Root;
                // 使用 RXEName 作为模板的唯一标识符

                if (root == null)
                {
                    _log.LogError("File is empty or not a valid XML: {}", file);
                    continue;
                }

                string? templateName = root?.Element("RXEName")?.Value?.Trim();

                if (!string.IsNullOrEmpty(templateName))
                {
                    _templates[templateName] = root!;
                    _log.LogDebug("Loaded template '{}' from {}", templateName, Path.GetFileName(file));
                }
                else
                {
                    // 也可以选择使用defName作为备用key
                    var defName = root?.Element("defName")?.Value?.Trim();
                    string fallbackName = defName ?? Path.GetFileNameWithoutExtension(file).Replace("Template", "");
                    _templates[fallbackName] = root!;
                    _log.LogDebug("Loaded template '{}' (using filename) from {}", fallbackName, Path.GetFileName(file));
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to load or parse {}", file);
            }
        }
    }

    /// <summary>
    /// 解析指定的模板，自动处理 rxe_parent 继承链。
    /// </summary>
    /// <param name="templateName"> 要解析的模板名称 (RXEName)。 </param>
    /// <returns> 包含最终合并后 XML 和元数据信息的 RXmlTemplate 对象。 </returns>
    public RXmlTemplate ParseTemplate(string templateName)
    {
        if (!_templates.ContainsKey(templateName))
        {
            throw new KeyNotFoundException($"Template '{templateName}' not found in the library.");
        }

        // 1. 解析继承链并合并 XML
        XElement resolvedRoot = ResolveInheritance(templateName, new HashSet<string>());

        // 2. 从合并后的 XML 中解析元数据
        List<NodeMeta> metadata = RXmlMetadataParser.Parse(resolvedRoot);

        return new RXmlTemplate
        {
            Root = resolvedRoot,
            Nodes = metadata
        };
    }

    /// <summary>
    /// 递归解析继承关系并返回合并后的 XElement。
    /// </summary>
    private XElement ResolveInheritance(string templateName, HashSet<string> visited)
    {
        if (!_templates.TryGetValue(templateName, out var childElement))
        {
            throw new KeyNotFoundException($"Parent template '{templateName}' not found.");
        }

        // 检测循环继承
        if (!visited.Add(templateName))
        {
            throw new InvalidOperationException($"Circular dependency detected in template inheritance: {string.Join(" -> ", visited)} -> {templateName}");
        }

        string? parentName = childElement.Attribute("rxe_parent")?.Value;

        if (string.IsNullOrEmpty(parentName))
        {
            // 没有父模板，返回当前模板的深拷贝
            return new XElement(childElement);
        }

        // 递归解析父模板
        XElement parentElement = ResolveInheritance(parentName, visited);

        // 合并父子模板
        XElement mergedElement = RXmlMerger.MergeTemplates(parentElement, childElement);

        // 清理当前路径的访问记录，以便其他分支可以正常访问
        visited.Remove(templateName);

        return mergedElement;
    }
}
