using MessagePack;

namespace RimXmlEdit.Core.Entries;

[MessagePackObject]
public class XmlFieldInfo
{
    /// <summary>
    ///     节点名称
    /// </summary>
    [Key(0)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     节点的类型文本
    /// </summary>
    [Key(1)]
    public string FieldTypeName { get; set; } = string.Empty;

    /// <summary>
    ///     节点的大致类型
    /// </summary>
    [Key(2)]
    public XmlFieldType Type { get; set; } = XmlFieldType.Unknown;

    /// <summary>
    ///     如果类型是 Enum，这里是所有枚举值
    /// </summary>
    [Key(3)]
    public List<string>? EnumValues { get; set; }

    /// <summary>
    ///     如果类型是 PolymorphicList，这里指向 DefCache.Comps 中的索引 ID
    /// </summary>
    [Key(4)]
    public List<int>? PossibleClassValues { get; set; }

// ================================================== 用于xml构建和序列化
    /// <summary> 用于指示节点值, 若为list, 则添加<li>; 若为复杂类型, 则为字典, 键为标签; </summary>
    [Key(5)]
    public object Value { get; set; } = null!;

    /// <summary>
    ///     当类型为List的时候, 这里可以选择使用Class作为指向
    /// </summary>
    [Key(6)]
    public string Ref { get; set; } = string.Empty;

    /// <summary>
    ///     1. 如果本节点是复杂对象(如GraphicData)，这里包含 graphicClass, texPath 等字段。
    ///     2. 如果本节点是 List&lt;T&gt;，这里包含 T 的字段结构 (用于指导编辑器生成 &lt;li&gt; 内部的内容)。
    /// </summary>
    [Key(7)]
    public List<XmlFieldInfo>? Children { get; set; }

    /// <summary>
    ///     指向 sidecar 文件中的 TypeSchema 索引。
    ///     -1 表示无结构/基本类型。
    /// </summary>
    [Key(8)]
    public int SchemaId { get; set; } = -1;

    [Key(9)] public bool IsHaveTranslationHandle { get; set; } = false;

    [Key(10)] public bool MustTranslate { get; set; } = false;

    [IgnoreMember] public string? TKey { get; set; } = null;

    public override string ToString()
    {
        return $"{Name}({FieldTypeName})";
    }
}

[MessagePackObject]
public class DefInfo
{
    [Key(0)] public string TagName { get; set; } = string.Empty;

    [Key(1)] public List<XmlFieldInfo> Fields { get; set; } = new();

    [Key(2)] public List<string>? EnumValues { get; set; }

// ================================================== 用于xml构建和序列化
    [Key(3)] public string Name { get; set; } = string.Empty;

    [Key(4)] public string ParentName { get; set; } = string.Empty;

    [Key(5)] public bool IsAbstract { get; set; } = false;

    [Key(6)] public bool IgnoreConfigErrors { get; set; } = false;

    /// <summary>
    ///     适配Operation
    /// </summary>
    [Key(7)]
    public string Ref { get; set; } = string.Empty;

    /// <summary>
    ///     适配 About
    /// </summary>
    [Key(8)]
    public string Value { get; set; } = string.Empty;

    /// <summary>
    ///     路径, 用于路径解析
    /// </summary>
    [Key(9)]
    public string FullName { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"{TagName}(Elements: {Fields.Count})";
    }
}

[MessagePackObject]
public class TypeSchema
{
    /// <summary>
    ///     类型的全名 (Key)
    /// </summary>
    [Key(0)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    ///     该类型包含的字段结构
    /// </summary>
    [Key(1)]
    public List<XmlFieldInfo> Fields { get; set; } = new();

    public override string ToString()
    {
        return FullName;
    }
}

[MessagePackObject]
public class PossibleClass
{
    [Key(0)] public string FullName { get; set; } = string.Empty;

    [Key(1)] public int SchemaId { get; set; } = -1;

    [Key(2)] public bool IsThingComp { get; set; } = false;
    
    [Key(3)] public List<XmlFieldInfo> Fields { get; set; }

    public override string ToString()
    {
        return FullName;
    }

    public bool Equals(PossibleClass other)
    {
        if (other == null) return false;
        return FullName == other.FullName && SchemaId == other.SchemaId && IsThingComp == other.IsThingComp;
    }
}