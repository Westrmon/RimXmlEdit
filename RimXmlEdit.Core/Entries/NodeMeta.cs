namespace RimXmlEdit.Core.Entries;

public class NodeMeta
{
    private string _content;

    /// <summary>
    /// 节点的完整 XML 路径, e.g., /ThingDef/statBases/Damage
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 节点名称, e.g., Damage
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 在节点内容中找到的占位符, e.g., damage (来自 {damage})
    /// </summary>
    public string Placeholder { get; set; }

    /// <summary>
    /// 描述信息 (来自 rxe_dec)
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// 数据类型 (来自 rxe_type)
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// 数值范围 (来自 rxe_range)
    /// </summary>
    public string Range { get; set; }

    /// <summary>
    /// 是否为列表，允许多个子项 (来自 rxe_list)
    /// </summary>
    public bool IsList { get; set; }

    /// <summary>
    /// 继承的父模板名称 (来自 rxe_parent)
    /// </summary>
    public string Parent { get; set; }

    /// <summary>
    /// 默认值 (来自 rxe_default)
    /// </summary>
    public string Default { get; set; }

    /// <summary>
    /// 是否为必填项 (来自 rxe_required)
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// 是否可编辑 (来自 rxe_editable)
    /// </summary>
    public bool Editable { get; set; } = true; // 默认为可编辑

    /// <summary>
    /// 是否在 UI 中隐藏 (来自 rxe_hide)
    /// </summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// 是否为文件 (来自 rxe_isfile)
    /// </summary>
    public bool IsFile { get; set; }

    /// <summary>
    /// 枚举的可选值列表 (来自 rxe_enum)
    /// </summary>
    public string[] EnumValues { get; set; }

    public string Content
    {
        get { return _content; }
        set
        {
            _content = value;
            var atts = _content.Split(';', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < atts.Length; i++)
            {
                var att = atts[i].Split('=', StringSplitOptions.RemoveEmptyEntries);
                RXExtraAttributes.Add(new(att[0], att[1]));
            }
        }
    }

    public List<RXExtraAttributes> RXExtraAttributes { get; set; }
}

public record class RXExtraAttributes(string Name, string Type)
{
    /// <summary>
    /// 设置值
    /// </summary>
    public string Value { get; set; } = "";
}
