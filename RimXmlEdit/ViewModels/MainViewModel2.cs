using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.NodeGeneration;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Models;
using RimXmlEdit.Utils;
using static RimXmlEdit.Core.NodeInfoManager;

namespace RimXmlEdit.ViewModels;

public partial class MainViewModel
{
    private readonly Lock _saveLock = new();
    private CancellationTokenSource? _debounceCts;
    private int _valueValidationInterval;

    // 进行初步验证输入值的合法性
    private async void CheckInputValue(object? sender, TextChangedEventArgs e)
    {
        // 需要置位正在加载, 若为true, 直接跳过
        if (DefNodeManager.IsLoading) return;
        if (sender is not TextBox textBox || !textBox.IsKeyboardFocusWithin) return;

        var textToValidate = textBox.Text;
        if (textBox.DataContext is not DefNode currentNode) return;

        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;

        try
        {
            await Task.Delay(500, token);
            var result = await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(textToValidate))
                    return CheckResult.Empty;

                return _nodeInfoManager.CheckValueIsValid(
                    currentNode.GetFullName(), textToValidate);
            }, token);

            if (result.IsValid)
            {
                currentNode.IsError = false;
                currentNode.ErrorMessage = null;
            }
            else
            {
                currentNode.IsError = true;
                currentNode.ErrorMessage = result.ValueTypeOrErrMs;
            }
        }
        catch (TaskCanceledException)
        {
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "CheckInputValue error");
        }
    }

    private void OnAutoSaveEvent(object? sender, ElapsedEventArgs e)
    {
        try
        {
            using (_saveLock.EnterScope())
            {
                if (string.IsNullOrEmpty(_currentFilePath)) return;
                
                _defManager.SaveToFile(_currentFilePath);
                _defDefineInfo.Save();
                _log.LogNotify("[{Time}] Success to save", DateTime.Now);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Save Error");
        }
    }

    private void FileExplorerOnOnCreateTemplate(object? sender, TemplateXmlViewModel e)
    {
        if (e.SelectedSecondaryCategory == null)
        {
            _log.LogError("Selected template name is null");
            return;
        }

        var filter = e.Options.Where(t => t.IsSelected).Select(g => g.Title);
        var args = $"{string.Join(';', filter)}";
        var blueprint = NodeGenerationService.Generate(
            DefNodeManager.IsPatch,
            ChildViewNode.TagName,
            e.SelectedSecondaryCategory.Name,
            args);
        var node = BuildFromBlueprint(blueprint, ChildViewNode, _setting.AutoExpandNodes);
        node.IsNodeExpanded = true;
        DefTreeNodes.Add(node);
    }

    internal static void HandleNodeAttributeChanged(object? sender, AttributeChangedEventArgs e)
    {
        if (DefNodeManager.IsLoading) return;
        if (e.Attribute.Name == "Class" && sender is DefNode node)
        {
            var newNodeBlueprint = NodeGenerationService.RefreshNodeStructure(
                DefNodeManager.IsPatch,
                node.Parent == DefNodeManager.RootNode ? DefNodeManager.RealRootNodeName : node.Parent.TagName,
                node.TagName,
                e.Attribute.Value.ToString());
            if (newNodeBlueprint.Equals(NodeBlueprint.None)) return;
            node.Children.Clear();
            BuildFromBlueprint(newNodeBlueprint, node, true);
        }
    }

    public static DefNode BuildFromBlueprint(
        NodeBlueprint blueprint,
        DefNode parent,
        bool haveRoot = false,
        bool autoExpend = false)
    {
        DefNode node;
        if (haveRoot)
        {
            node = parent;
        }
        else
        {
            node = new DefNode(blueprint.TagName, parent);
            node.IsNodeExpanded = autoExpend;
            node.AttributeChanged += HandleNodeAttributeChanged;
        }

        if (blueprint.Value != null) node.Value = blueprint.Value;
        foreach (var attr in blueprint.Attributes)
        {
            var attrVm = new DefAttributeViewModel(node, attr.Name, attr.Value)
            {
                IsEnum = attr.IsEnum,
                EnumList = attr.EnumList
            };
            node.AddAttribute(attrVm);
        }

        foreach (var childBlueprint in blueprint.Children)
        {
            var childNode = BuildFromBlueprint(childBlueprint, node);
            node.Children.Add(childNode);
        }

        return node;
    }

    public void UpdataSetting()
    {
        if (_autoSaveTimer.Interval != _setting.AutoSaveInterval * 60000)
            _autoSaveTimer.Interval = _setting.AutoSaveInterval * 60000;
        _valueValidationInterval = _setting.ValueValidationInterval;
        LoggerFactoryInstance.SetLevels(_setting.FileLoggingLevel, _setting.NotificationLoggingLevel);
    }
}