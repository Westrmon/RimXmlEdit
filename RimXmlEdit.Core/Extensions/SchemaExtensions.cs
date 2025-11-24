namespace RimXmlEdit.Core.Extensions;

using RimXmlEdit.Core.Entries;
using System.Collections.Generic;
using System.Linq;

public static class SchemaExtensions
{
    /// <summary>
    /// 获取当前字段的子字段列表。 优先返回实例自带的 Children；如果为空，则尝试通过 SchemaId 解析。
    /// </summary>
    /// <param name="field"> 当前的字段节点 </param>
    /// <param name="schemas"> 从 sidecar 加载的所有 Schema 列表 </param>
    /// <returns> 子字段列表，如果没有则返回空列表 </returns>
    public static List<XmlFieldInfo> ResolveChildren(this XmlFieldInfo field, List<TypeSchema> schemas)
    {
        if (field.Children != null && field.Children.Count > 0)
        {
            return field.Children;
        }

        if (field.SchemaId >= 0 && schemas != null && field.SchemaId < schemas.Count)
        {
            return schemas[field.SchemaId].Fields;
        }

        return new List<XmlFieldInfo>();
    }

    /// <summary>
    /// 想要编辑一个字段时，从 Schema 创建一份全新的、可修改的副本。
    /// </summary>
    public static List<XmlFieldInfo> InstantiateFromSchema(this XmlFieldInfo field, List<TypeSchema> schemas)
    {
        var originals = field.ResolveChildren(schemas);
        if (originals.Count == 0) return new List<XmlFieldInfo>();

        // 深拷贝第一层，保持引用关系以便下一层继续 Lazy Load
        return originals.Select(f => new XmlFieldInfo
        {
            Name = f.Name,
            FieldTypeName = f.FieldTypeName,
            Type = f.Type,
            EnumValues = f.EnumValues, // List<string> 是引用类型，通常只读，直接引用即可
            PossibleClassValues = f.PossibleClassValues,
            SchemaId = f.SchemaId, // 关键：保留 SchemaId，使得子字段可以继续被解析
            Value = null, // 实例化的新对象，值为空
            Ref = f.Ref
        }).ToList();
    }
}
