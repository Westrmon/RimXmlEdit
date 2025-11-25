using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;

namespace RimXmlEdit.Models;

public interface ICommittableSetting
{
    void Commit();
}

public abstract partial class SettingItemBase : ObservableObject, ICommittableSetting
{
    public string Label { get; set; }
    public string Description { get; set; }

    [ObservableProperty]
    protected bool _isUsed = true;

    public abstract void Commit();
}

public abstract partial class SettingItem<T> : SettingItemBase
{
    [ObservableProperty]
    protected T _value;

    // 核心：保存时的回调动作
    private readonly Action<T> _onCommit;

    protected SettingItem(T initialValue, Action<T> onCommit)
    {
        _value = initialValue;
        _onCommit = onCommit;
    }

    public override void Commit()
    {
        if (IsUsed)
        {
            _onCommit?.Invoke(Value);
        }
    }
}

public partial class TextSettingItem : SettingItem<string>
{
    public string Watermark { get; set; }
    public bool IsMultiline { get; set; }

    public TextSettingItem(string label, string value, Action<string> onCommit)
        : base(value, onCommit)
    {
        Label = label;
    }
}

public partial class BoolSettingItem : SettingItem<bool>
{
    public BoolSettingItem(string label, bool value, Action<bool> onCommit)
        : base(value, onCommit)
    {
        Label = label;
    }
}

public partial class NumberSettingItem : SettingItem<int>
{
    public int Min { get; set; } = 0;
    public int Max { get; set; } = 100;

    public NumberSettingItem(string label, int value, Action<int> onCommit)
        : base(value, onCommit)
    {
        Label = label;
    }
}

public partial class EnumSettingItem : SettingItem<string>
{
    public IEnumerable<string> EnumValues { get; set; }

    public EnumSettingItem(string label, string value, Action<string> onCommit, IEnumerable<string> enumValues)
        : base(value, onCommit)
    {
        Label = label;
        EnumValues = enumValues;
    }
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
