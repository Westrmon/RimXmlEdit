using System.Xml.Linq;

namespace RimXmlEdit.Core.XmlOperator;

internal static class RXmlMerger
{
    /// <summary>
    /// 将子模板合并到父模板上。 规则：
    /// 1. 子节点的属性会覆盖父节点的同名属性。
    /// 2. 子节点中存在的元素会替换父节点中的同名元素。
    /// 3. 子节点中新增的元素会被追加。
    /// 4. 父节点独有的元素会被保留。
    /// </summary>
    /// <param name="parent"> 父模板 XElement 的一个副本。 </param>
    /// <param name="child"> 子模板 XElement。 </param>
    /// <returns> 一个新的、合并后的 XElement。 </returns>
    public static XElement MergeTemplates(XElement parent, XElement child)
    {
        // 创建父模板的深拷贝以避免修改原始缓存
        XElement merged = new XElement(parent);

        // 1. 合并属性：子的覆盖父的
        foreach (var childAttr in child.Attributes())
        {
            merged.SetAttributeValue(childAttr.Name, childAttr.Value);
        }

        // 2. 合并子节点
        foreach (var childNode in child.Elements())
        {
            var parentNode = merged.Elements(childNode.Name).FirstOrDefault();

            if (parentNode != null)
            {
                // 如果父节点中存在同名节点，则递归合并
                var recursivelyMergedNode = MergeTemplates(parentNode, childNode);
                parentNode.ReplaceWith(recursivelyMergedNode);
            }
            else
            {
                // 如果父节点中不存在，则直接添加
                merged.Add(childNode);
            }
        }

        // 如果子节点有直接文本内容，则使用子节点的
        if (!child.Elements().Any() && !string.IsNullOrEmpty(child.Value))
        {
            merged.Value = child.Value;
        }

        return merged;
    }
}
