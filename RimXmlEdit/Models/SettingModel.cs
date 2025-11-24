using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace RimXmlEdit.Models;

public partial class SettingItemBase : ObservableObject
{
    public string Label { get; set; }
    public string Description { get; set; }

    [ObservableProperty]
    protected bool _isUsed = true;
}

public partial class TextSettingItem : SettingItemBase
{
    [ObservableProperty] private EditableString _value;
    public string Watermark { get; set; }
    public bool IsMultiline { get; set; } // 用于区分单行还是多行文本框
}

public partial class BoolSettingItem : SettingItemBase
{
    [ObservableProperty] private bool _value;
}

public partial class NumberSettingItem : SettingItemBase
{
    [ObservableProperty] private int _value;
    public int Min { get; set; } = 0;
    public int Max { get; set; } = 100;
}

public partial class EnumSettingItem : SettingItemBase
{
    [ObservableProperty] private string _value;
    public IEnumerable<string> EnumValues { get; set; }
}

// --- 侧边栏导航项 ---
public class NavigationItem
{
    public string Title { get; set; }
    public string TargetControlName { get; set; } // 对应 XAML 中的 x:Name
}

public partial class EditableString : ObservableObject
{
    [ObservableProperty]
    private string _value;

    public EditableString(string value)
    {
        _value = value;
    }
}
