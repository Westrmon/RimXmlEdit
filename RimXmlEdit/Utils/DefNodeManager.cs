using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Core.XmlOperator;
using RimXmlEdit.Models;
using RimXmlEdit.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading;

namespace RimXmlEdit.Utils;

internal partial class DefNodeManager
{
    private bool ingoreError = true;

    private static readonly Lock _lock = new Lock();
    private readonly ILogger _log;
    private readonly ObservableCollection<DefNode> _defNodes;
    private readonly AppSettings _settings;
    private static DefNode? _rootNode;

    public static bool IsLoading { get; private set; } = false;

    public static bool IsPatch { get; private set; }

    public static DefNode RootNode
    {
        get
        {
            using (_lock.EnterScope())
            {
                _rootNode ??= new DefNode("Root", null!);
                return _rootNode;
            }
        }
    }

    public static string RealRootNodeName
        => IsPatch ? "Patch" : "Defs";

    public DefNodeManager(ObservableCollection<DefNode> defNodes, AppSettings settings)
    {
        _log = this.Log();
        _defNodes = defNodes;
        _settings = settings;
        WeakReferenceMessenger.Default.Register<FlushViewMessage>(this, (_, m) =>
        {
            if (m.Sender?.Target is DefNode node)
                _defNodes.Remove(node);
        });
    }

    //public void AddNode(DefNode parentNode, DefNode newNode)
    //{
    //    if (parentNode.TagName == "Root")
    //    {
    //        _defNodes.Add(newNode);
    //    }
    //    else
    //    {
    //        newNode.IsEditable = true;
    //        parentNode.Children.Add(newNode);
    //    }
    //}

    public async void SaveToFile(string path, string? xmlContent = null)
    {
        try
        {
            xmlContent ??= ConvertToXml();
            await File.WriteAllTextAsync(path, xmlContent);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Cant save file");
        }
    }

    public void LoadFromExtendXml(string path)
    {
        IsLoading = true;
        if (!File.Exists(path) || !path.EndsWith(".xml"))
        {
            _log.LogError("文件无效或不存在: {Path}", path);
            return;
        }

        try
        {
            var txt = File.ReadAllText(path);
            IsPatch = Path.GetRelativePath(TempConfig.ProjectPath, path).Contains("Patches");
            ConvertToDefNode(txt);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "加载 XML 失败");
            IsLoading = false;
        }
    }

    // 可以通过设置, 来确定是否可以转化错误节点(可能本来就没错)
    public string ConvertToXml()
    {
        var rxStruct = new RXStruct { IsPatch = IsPatch };
        foreach (var node in _defNodes)
        {
            if (node.IsError)
                continue;
            var def = node.ToDefInfo();
            rxStruct.Defs.Add(def);
            BuildRXStruct(def.Fields, node.Children);
        }
        return XmlConverter.Serialize(rxStruct);
    }

    public void ConvertToDefNode(string txt)
    {
        IsLoading = true;

        var tempRootNodes = new List<DefNode>();

        try
        {
            if (string.IsNullOrEmpty(txt))
            {
                if (IsPatch)
                    tempRootNodes.Add(CreateNode("Operation", RootNode));
            }
            else if (XmlConverter.Deserialize(txt) is RXStruct rxStruct)
            {
                IsPatch = rxStruct.IsPatch;
                var currentXmlStructs = rxStruct;
                // 解析第一层的节点, 一般都含有各种属性
                foreach (var def in rxStruct.Defs)
                {
                    var node = CreateNode(def.TagName, RootNode, def.Value as string);

                    if (!string.IsNullOrEmpty(def.ParentName))
                        node.Attributes.Add(new DefAttributeViewModel(node, "ParentName", def.ParentName));
                    if (def.IsAbstract)
                        node.Attributes.Add(new DefAttributeViewModel(node, "Abstract", def.IsAbstract, true));
                    if (def.IgnoreConfigErrors)
                        node.Attributes.Add(new DefAttributeViewModel(node, "IgnoreConfigErrors", def.IgnoreConfigErrors, true));
                    if (!string.IsNullOrEmpty(def.Ref))
                    {
                        var attr = new DefAttributeViewModel(node, "Class", def.Ref);
                        if (IsPatch)
                            attr.EnumList = NodeInfoManager.PatchesClassEnums;
                        node.Attributes.Add(attr);
                    }

                    BuildNodes(node, node.Children, def.Fields);

                    tempRootNodes.Add(node);
                }
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "XML 解析转换为节点失败");
        }
        finally
        {
            _defNodes.Clear();
            foreach (var node in tempRootNodes)
            {
                _defNodes.Add(node);
            }

            IsLoading = false;
        }
    }

    private void BuildNodes(DefNode root, ObservableCollection<DefNode> nodeList, object? values)
    {
        if (values == null) return;

        if (values is IReadOnlyDictionary<string, XmlFieldInfo> dictInfo)
        {
            foreach (var kvp in dictInfo)
            {
                var node = CreateNode(kvp.Key, root, kvp.Value?.Value as string);

                // 注意: 只有当 Name 确实承载内容时才赋值 Value，否则可能会覆盖掉标签内容
                if (kvp.Value?.Name != null && kvp.Value.Name != kvp.Key)
                    node.Value = kvp.Value.Name;

                if (!string.IsNullOrEmpty(kvp.Value?.Ref))
                {
                    var attr = new DefAttributeViewModel(node, "Class", kvp.Value.Ref);
                    if (IsPatch)
                        attr.EnumList = NodeInfoManager.PatchesClassEnums;
                    node.Attributes.Add(attr);
                }

                if (kvp.Value?.Value != null)
                {
                    if (kvp.Value.Value is string value)
                        node.Value = value;
                    else
                        BuildNodes(node, node.Children, kvp.Value.Value);
                }

                nodeList.Add(node);
            }
            return;
        }
        if (values is System.Collections.IEnumerable list && values is not string)
        {
            foreach (var item in list)
            {
                if (item is XmlFieldInfo info)
                {
                    var node = CreateNode(info.Name, root, info.Value as string);
                    if (!string.IsNullOrEmpty(info.Ref))
                    {
                        var attr = new DefAttributeViewModel(node, "Class", info.Ref);
                        if (IsPatch)
                            attr.EnumList = NodeInfoManager.PatchesClassEnums;
                        node.Attributes.Add(attr);
                    }

                    if (info.Value != null)
                    {
                        if (info.Value is string value)
                        {
                            node.Value = value;
                        }
                        else
                        {
                            BuildNodes(node, node.Children, info.Value);
                        }
                    }
                    nodeList.Add(node);
                }
            }
            return;
        }
        if (values is XmlFieldInfo singleInfo)
        {
            var node = CreateNode(singleInfo.Name, root, singleInfo.Value as string);
            if (!string.IsNullOrEmpty(singleInfo.Ref))
            {
                var attr = new DefAttributeViewModel(node, "Class", singleInfo.Ref);
                if (IsPatch)
                    attr.EnumList = NodeInfoManager.PatchesClassEnums;
                node.Attributes.Add(attr);
            }

            if (singleInfo.Value != null)
            {
                if (singleInfo.Value is string value)
                    node.Value = value;
                else
                    BuildNodes(node, node.Children, singleInfo.Value);
            }
            nodeList.Add(node);
        }
    }

    public DefNode CreateNode(string tagName, DefNode parent, string? value = null, EventHandler<AttributeChangedEventArgs>? init = null)
    {
        var node = new DefNode(tagName, parent);
        node.Value = value;
        node.IsNodeExpanded = _settings.AutoExpandNodes;
        node.AttributeChanged += MainViewModel.HandleNodeAttributeChanged;
        return node;
    }

    private void BuildRXStruct(List<XmlFieldInfo> fields, IEnumerable<DefNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.IsError && !ingoreError)
                continue;

            var fieldInfo = node.ToXmlFieldInfo();

            if (node.HasChildren)
            {
                var childFields = new List<XmlFieldInfo>();
                BuildRXStruct(childFields, node.Children);
                fieldInfo.Value = childFields;
            }

            fields.Add(fieldInfo);
        }
    }
}

public static class DefNodeConvert
{
    public static DefInfo ToDefInfo(this DefNode node)
    {
        var defInfo = new DefInfo()
        {
            TagName = node.TagName,
            Fields = new List<XmlFieldInfo>(),
        };

        if (node.Attributes.Count > 0)
        {
            foreach (var att in node.Attributes)
            {
                switch (att.Name)
                {
                    case "ParentName": defInfo.ParentName = (string)att.Value; break;
                    case "Abstract": defInfo.IsAbstract = (bool)att.Value; break;
                    case "IgnoreConfigErrors": defInfo.IgnoreConfigErrors = (bool)att.Value; break;
                    case "Class": defInfo.Ref = (string)att.Value; break;
                }
            }
        }
        return defInfo;
    }

    public static XmlFieldInfo ToXmlFieldInfo(this DefNode node)
    {
        var fieldInfo = new XmlFieldInfo()
        {
            Name = node.TagName,
            Value = node.Value
        };
        if (node.Attributes.Count > 0)
        {
            var att = node.Attributes.FirstOrDefault(t => t.Name == "Class");
            fieldInfo.Ref = att == null ? string.Empty : (string)att.Value;
        }
        return fieldInfo;
    }
}
