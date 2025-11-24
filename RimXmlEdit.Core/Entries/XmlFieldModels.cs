namespace RimXmlEdit.Core.Entries;

public enum XmlFieldType : byte
{
    /// <summary>
    /// 未知或不支持
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// 简单类型 (int, string, float...)
    /// </summary>
    Primitive,

    /// <summary>
    /// 枚举
    /// </summary>
    Enumable = 4,

    /// <summary>
    /// RimWorld 内定义的简单类 (如 IntVec3)
    /// </summary>
    SimpleClass = 8,

    /// <summary> 具体类型的列表 (如 List<string> 或 List<ThingDefCountClass>) </summary>
    SimpleList = 16,

    /// <summary> 多态列表，使用 Class="..." 属性 (如 List<CompProperties>) </summary>
    PolymorphicList = 32,

    /// <summary>
    /// 元数据
    /// </summary>
    MetaData = 64,

    /// <summary>
    /// 非Def派生, 但是是Field关联类型
    /// </summary>
    NonDefType = 128
}
