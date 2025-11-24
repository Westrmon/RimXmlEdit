using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RimXmlEdit.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RimXmlEdit.Models;

public partial class DefAttributeViewModel : ViewModelBase
{
    public event EventHandler OnRemoveAttribute;

    private DefNode _parentNode;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private object _value;

    [ObservableProperty]
    private IEnumerable<string>? _enumList;

    [ObservableProperty]
    private bool _isEnum;

    public DefAttributeViewModel(DefNode parentNode, string name, object defaultValue, bool isBool = false)
    {
        _parentNode = parentNode;
        Name = name;
        Value = defaultValue;
        OnRemoveAttribute += RemoveInner;
        if (isBool)
        {
            EnumList = new List<string> { "True", "False" };
        }
    }

    [RelayCommand]
    private void RemoveAttribute()
    {
        OnRemoveAttribute?.Invoke(this, EventArgs.Empty);
    }

    internal void RemoveInner(object? sender, EventArgs e)
        => _parentNode.RemoveAttribute(sender as DefAttributeViewModel);

    partial void OnEnumListChanged(IEnumerable<string>? value)
    {
        IsEnum = value?.Any() ?? false;
    }

    partial void OnValueChanged(object value)
    {
        // 通知父节点属性发生了改变
        _parentNode.OnAttributeChanged(this);
    }
}
