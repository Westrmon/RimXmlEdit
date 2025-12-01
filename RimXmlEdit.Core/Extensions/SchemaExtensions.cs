using RimXmlEdit.Core.Entries;

namespace RimXmlEdit.Core.Extensions;

public static class SchemaExtensions
{
    /// <summary>
    ///     获取当前字段的子字段列表。 优先返回实例自带的 Children；如果为空，则尝试通过 SchemaId 解析。
    /// </summary>
    /// <param name="field"> 当前的字段节点 </param>
    /// <param name="schemas"> 从 sidecar 加载的所有 Schema 列表 </param>
    /// <returns> 子字段列表，如果没有则返回空列表 </returns>
    public static List<XmlFieldInfo> ResolveChildren(this XmlFieldInfo field, List<TypeSchema> schemas)
    {
        if (field.Children != null && field.Children.Count > 0) return field.Children;

        if (field.SchemaId >= 0 && schemas != null && field.SchemaId < schemas.Count)
            return schemas[field.SchemaId].Fields;

        return new List<XmlFieldInfo>();
    }

    public static List<XmlFieldInfo> ResolveChildren(this PossibleClass comp, List<TypeSchema> schemas)
    {
        if (comp.SchemaId >= 0 && schemas != null && comp.SchemaId < schemas.Count)
            return schemas[comp.SchemaId].Fields;

        return new List<XmlFieldInfo>();
    }

    /// <summary>
    ///     想要编辑一个字段时，从 Schema 创建一份全新的、可修改的副本。
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
            EnumValues = f.EnumValues,
            SchemaId = f.SchemaId,
            Value = null,
            Ref = f.Ref
        }).ToList();
    }
}