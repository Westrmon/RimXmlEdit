using MessagePack;

namespace RimXmlEdit.Core.Entries;

[MessagePackObject]
public class RXStruct
{
    /// <summary> 此节点是否是补丁节点, 决定Root是否为<Patch> </summary>
    [Key(0)]
    public bool IsPatch { get; set; } = false;

    [Key(1)] public bool IsModMetaData { get; set; } = false;

    /// <summary>
    ///     定义节点信息
    /// </summary>
    [Key(2)]
    public List<DefInfo> Defs { get; set; } = new();

    [Key(3)] public string FilePath { get; set; } = string.Empty;
}