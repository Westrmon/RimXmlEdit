using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using RimXmlEdit.Core;
using RimXmlEdit.Utils;

namespace RimXmlEdit.Models;

/// <summary>
///     Represents a single node in the XML Definition tree structure.
/// </summary>
public partial class DefNode : ObservableObject
{
    [ObservableProperty] private string? _errorMessage;

    private bool _isEditable = true;

    [DefaultValue(false)] [ObservableProperty]
    private bool _isError;

    [DefaultValue(false)] [ObservableProperty]
    private bool _isNodeExpanded;

    [ObservableProperty] private string _tagName = string.Empty;

    [ObservableProperty] private string? _value;

    [ObservableProperty] private bool isClosedTag; // e.g., </defName>

    public DefNode(string name, DefNode parent)
    {
        _tagName = name;
        Parent = parent;
        if (name != "Root")
            RequestDelete += parent.OnChildRequestDelete;
        //Parent?.Children.Add(this);
        Children.CollectionChanged += Children_CollectionChanged;
    }

    public bool IsEditable
    {
        get => _isEditable || Value != null;
        set
        {
            _isEditable = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    ///     Gets the collection of child nodes, enabling the hierarchical view.
    /// </summary>
    public ObservableCollection<DefNode> Children { get; set; } = [];

    public ObservableCollection<DefAttributeViewModel> Attributes { get; set; } = new();

    public DefNode Parent { get; set; }

    /// <summary>
    ///     Gets a value indicating whether this node has children and should be expandable.
    /// </summary>
    [XmlIgnore]
    public bool HasChildren => Children.Count > 0;

    public bool IsCanRefTag => true;
    public event EventHandler? RequestDelete;

    public event EventHandler<AttributeChangedEventArgs>? AttributeChanged;

    [RelayCommand]
    private void AddAttribute(string attributeName)
    {
        if (string.IsNullOrWhiteSpace(attributeName)) return;
        if (Attributes.Any(x => x.Name.Equals(attributeName, StringComparison.OrdinalIgnoreCase)))
            return;
        DefAttributeViewModel attrVm;

        if (attributeName.Equals("Abstract", StringComparison.OrdinalIgnoreCase))
        {
            attrVm = new DefAttributeViewModel(this, attributeName, "False", true);
        }
        else if (attributeName.Equals("Class", StringComparison.OrdinalIgnoreCase))
        {
            // li标签class属性依托于上一class属性, 暂时搁置
            if ((DefNodeManager.IsPatch && TagName == "Operation") || TagName == "match")
                attrVm = new DefAttributeViewModel(this, attributeName, "")
                {
                    EnumList = NodeInfoManager.PatchesClassEnums
                };
            else
                attrVm = new DefAttributeViewModel(this, attributeName, "");
        }
        else if (attributeName.Equals("ParentName", StringComparison.OrdinalIgnoreCase))
        {
            attrVm = new DefAttributeViewModel(this, attributeName, "");
        }
        else
        {
            return;
        }

        Attributes.Add(attrVm);
    }

    [RelayCommand]
    private void DeleteNode()
    {
        RequestDelete?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void DropFile()
    {
        // 主要绑定图层关系, 此处为被动
    }

    public void AddAttribute(DefAttributeViewModel attr)
    {
        Attributes.Add(attr);
    }

    // 没有子节点的叶子节点没有值
    private void Children_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add)
        {
            if (Value != null)
                Value = null;
            if (IsEditable)
                IsEditable = false;
        }
    }

    public string GetFullName(char split = '/', bool addClasses = false)
    {
        var sb = new StringBuilder(3);
        var node = this;
        do
        {
            sb.Insert(0, $"{split}{node.TagName}{(addClasses
                ? GetClassesAttribute(node)
                : string.Empty)}");
            node = node.Parent;
        } while (node != null && node.TagName != "Root");

        return sb.Remove(0, 1).ToString();
    }

    private void OnChildRequestDelete(object? sender, EventArgs e)
    {
        if (sender is DefNode child)
        {
            child.RequestDelete -= OnChildRequestDelete;
            if (TagName == "Root")
                WeakReferenceMessenger.Default.Send(new FlushViewMessage { Sender = new WeakReference(child) });
            else
                Children.Remove(child);
        }
    }

    private static string GetClassesAttribute(DefNode node)
    {
        var classes = node.Attributes.FirstOrDefault(t => t.Name == "Class")?.Value as string;
        return string.IsNullOrEmpty(classes) ? string.Empty : $"#{classes}";
    }

    partial void OnTagNameChanged(string value)
    {
        OnPropertyChanged(nameof(IsCanRefTag));
    }

    public void RemoveAttribute(DefAttributeViewModel attr)
    {
        if (Attributes.Contains(attr))
        {
            attr.OnRemoveAttribute -= attr.RemoveInner;
            Attributes.Remove(attr);
        }
    }

    internal void OnAttributeChanged(DefAttributeViewModel defAttributeViewModel)
    {
        AttributeChanged?.Invoke(this, new AttributeChangedEventArgs(defAttributeViewModel));
    }
}

public class AttributeChangedEventArgs : EventArgs
{
    public AttributeChangedEventArgs(DefAttributeViewModel attribute)
    {
        Attribute = attribute;
    }

    public DefAttributeViewModel Attribute { get; }
}