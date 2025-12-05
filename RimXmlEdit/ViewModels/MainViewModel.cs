using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RimXmlEdit.Core;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.NodeDefine;
using RimXmlEdit.Core.NodeGeneration;
using RimXmlEdit.Models;
using RimXmlEdit.Service;
using RimXmlEdit.Utils;
using RimXmlEdit.Views;

namespace RimXmlEdit.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private static NodeGenerationService NodeGenerationService;

    private readonly Timer _autoSaveTimer;
    private readonly DefNodeManager _defManager;

    private readonly ISimpleFileExplorer _fileExplorer;

    private readonly ILocalizationService _localizationService;
    private readonly ILogger _log;
    private readonly NodeInfoManager _nodeInfoManager;

    private readonly AppSettings _setting;

    [DefaultValue(true)] [ObservableProperty]
    private bool _canEditNodeDefine;

    private IEnumerable<string> _childNodes;

    [ObservableProperty] private string _childSiderBarSelectItem;

    private DefNode _childViewNode = DefNodeManager.RootNode;

    [ObservableProperty] private string _currentDescription = string.Empty;

    private string _currentFilePath = string.Empty;
    private NodeDefineInfo _defDefineInfo;

    [ObservableProperty] private DefNode _defNode;

    [ObservableProperty] private ObservableCollection<DefNode> _defTreeNodes = new();

    [ObservableProperty] private ObservableCollection<string> _filteredChildItems = new();

    private bool _isAddNodeToRoot;
    private bool _isInternalUpdate;

    [ObservableProperty] private bool _isNodeEditingTabSelected = true;

    [ObservableProperty] private string _nodeXmlContent = string.Empty;

    [ObservableProperty] private string _searchChildText = string.Empty;

    private string _searchDescNodeFullName = string.Empty;

    private bool _xmlIsChanged;

    [ObservableProperty] private string currentTabHeader = "节点编辑";

    /// <summary>
    ///     Initializes a new instance of the <see cref="MainViewModel" /> class.
    /// </summary>
    public MainViewModel(
        IOptions<AppSettings> setting,
        ILocalizationService localizationService,
        ISimpleFileExplorer fileExplorer,
        IQuickSearch quickSearch,
        NodeInfoManager nodeInfoManager,
        NodeGenerationService nodeGenerationService)
    {
        _log = this.Log();
        _localizationService = localizationService;
        _fileExplorer = fileExplorer;
        _nodeInfoManager = nodeInfoManager;
        NodeGenerationService ??= nodeGenerationService;
        _setting = setting.Value;
        _defManager = new DefNodeManager(DefTreeNodes, _setting);
        _localizationService.OnLanguageChanged += OnCultureChanged;
        GlobalSingletonHelper.OnApplicationExiting += () => OnAutoSaveEvent(null, null);
        MainWindow.OnDoubleTapped += (s, e) =>
        {
            if (e.SourceTypeName == nameof(TextBox))
                CanEditNodeDefine = true;
        };
        MainWindow.OnDoubleTapped += (s, e) =>
        {
            if (e.SourceTypeName != nameof(ListBox))
                return;
            InsertNodeFromSideBar();
        };
        MainWindow.OnInputNodeValue += CheckInputValue;
        quickSearch.OnItemSelected += InsertNodeFromSideBar;
        _fileExplorer.OnOpenFile += (s, e) =>
        {
            OnAutoSaveEvent(null, null);
            if (e.FullName.EndsWith("About.xml"))
            {
                GlobalSingletonHelper.Launcher.LaunchFileInfoAsync(new FileInfo(e.FullName));
                return;
            }

            _currentFilePath = e.FullName;
            _childViewNode = DefNodeManager.RootNode;
            _defManager.LoadFromExtendXml(_currentFilePath);
            SearchChildText = string.Empty;
            _ = UpdateChildList();
        };

        AppSettings.OnSettingChanged += UpdataSetting;

        _valueValidationInterval = _setting.ValueValidationInterval;

        _autoSaveTimer = new Timer();
        _autoSaveTimer.Interval = _setting.AutoSaveInterval * 60000; // 1分钟 (毫秒)
        _autoSaveTimer.AutoReset = true; // 重复执行
        _autoSaveTimer.Elapsed += OnAutoSaveEvent;
        _autoSaveTimer.Start();

        Task.Run(() =>
        {
            _nodeInfoManager.Init();
            OnCultureChanged(this, CultureInfo.CurrentCulture);
        });
    }

    private void OnCultureChanged(object? sender, CultureInfo culture)
    {
        _defDefineInfo = new NodeDefineInfo(_setting, _nodeInfoManager.DataCache, culture.Name);
        _defDefineInfo.Init();
    }

    private void InsertNodeFromSideBar(string? insertNodeName = null)
    {
        if (_childViewNode.IsError)
        {
            _childViewNode.IsError = false;
            _childViewNode.ErrorMessage = null;
        }

        var blueprint = NodeGenerationService.Generate(
            DefNodeManager.IsPatch,
            _childViewNode.TagName,
            insertNodeName ?? ChildSiderBarSelectItem,
            string.Empty);
        var node = BuildFromBlueprint(blueprint, _childViewNode);
        node.IsNodeExpanded = true;
        //DefNode node;
        //if (_childViewNode.TagName == "comps")
        //{
        //    node = new DefNode("li", _childViewNode);
        //    node.AddAttribute(new DefAttributeViewModel(node, "Class", ChildSiderBarSelectItem));
        //}
        //else
        //{
        //    node = new DefNode(ChildSiderBarSelectItem, _childViewNode);
        //}
        if (_childViewNode == DefNodeManager.RootNode)
            DefTreeNodes.Add(node);
        else
            _childViewNode.Children.Add(node);
        SearchChildText = string.Empty;
        FlushChildListAfterAdd();
    }

    partial void OnSearchChildTextChanged(string value)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnSearchChildTextChanged(value));
            return;
        }

        FilteredChildItems.Clear();
        if (_childNodes == null || !_childNodes.Any())
            return;
        if (string.IsNullOrWhiteSpace(value))
        {
            foreach (var item in _childNodes) FilteredChildItems.Add(item);
        }
        else
        {
            var filtered = _childNodes.Where(p => p.Contains(value, StringComparison.OrdinalIgnoreCase));
            foreach (var project in filtered) FilteredChildItems.Add(project);
        }

        _log.LogInformation("Filtered child node list with search term '{SearchTerm}'. Found {Count} results.", value,
            FilteredChildItems.Count);
    }

    partial void OnDefNodeChanged(DefNode value)
    {
        if (value != null) UpdateDescription(value.GetFullName('.'));
    }

    [RelayCommand]
    private async Task UpdateChildList()
    {
        var contextNode = _isAddNodeToRoot ? DefNodeManager.RootNode : DefNode ?? DefNodeManager.RootNode;
        IEnumerable<string>? childs = null;
        var fullName = string.Empty;

        _isAddNodeToRoot = false;
        await Task.Run(() =>
        {
            if (contextNode.TagName == "Root")
            {
                if (DefNodeManager.IsPatch)
                    childs = ["Operation"];
                else
                    childs = _nodeInfoManager.GetRootList();
            }
            else if (!DefNodeManager.IsPatch)
            {
                fullName = contextNode.GetFullName();
                childs = _nodeInfoManager.GetChildNameOrEnumValues(fullName);

                if (childs != null && contextNode.Children != null)
                {
                    var existingTags = contextNode.Children.Select(t => t.TagName).ToHashSet();
                    if (existingTags.Count == 1 && existingTags.TryGetValue("li", out _))
                    {
                        var existingClass = new HashSet<string>(StringComparer.Ordinal);
                        if (contextNode.Children != null)
                            foreach (var child in contextNode.Children)
                            {
                                if (child.Attributes == null) continue;

                                foreach (var attr in child.Attributes)
                                    if (attr.Name == "Class" && attr.Value is string val)
                                        existingClass.Add(val);
                            }

                        childs = childs.Where(c => !existingClass.Contains(c));
                    }
                    else
                    {
                        childs = childs.Where(c => c == "li" || !existingTags.Contains(c));
                    }
                }
            }
            else
            {
                childs = ["li", "value", "match", "operations", "xpath"];
            }
        });

        if (childs == null)
            _childNodes = [];
        else
            _childNodes = childs.OrderBy(c => c);
        if (_childNodes.Any())
            _childViewNode = contextNode;
        OnSearchChildTextChanged(SearchChildText);
    }

    [RelayCommand]
    private async Task AddNodeToRoot()
    {
        _childViewNode = DefNodeManager.RootNode;
        _isAddNodeToRoot = true;
        await UpdateChildList();
    }

    // --- Commands for interactions ---

    [RelayCommand]
    private void Save()
    {
        OnAutoSaveEvent(null, null);
    }

    [RelayCommand]
    private async Task ImportXml()
    {
        var file = await GlobalSingletonHelper.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Xml Files")
                {
                    Patterns = new[] { "*.xml" },
                    AppleUniformTypeIdentifiers = new[] { "public.xml" },
                    MimeTypes = new[] { "text/xml" }
                }
            },
            Title = "导入xml文件"
        });
        if (file is null) return;
        var path = file.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrEmpty(path)) return;

        OnAutoSaveEvent(null, null);
        _currentFilePath = path;

        _childViewNode = DefNodeManager.RootNode;
        _defManager.LoadFromExtendXml(path);
    }

    [RelayCommand]
    private async Task ImportDll()
    {
        var file = await GlobalSingletonHelper.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("dll Files") { Patterns = new[] { "*.dll" } }
            },
            Title = "Import dll File"
        });
        if (file is null) return;
        var path = file.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrEmpty(path)) return;
        _nodeInfoManager.AddDll(path);
    }

    [RelayCommand]
    private void Setting()
    {
        var setting = GlobalSingletonHelper.Service.GetRequiredService<SettingsView>();
        var vm = GlobalSingletonHelper.Service.GetRequiredService<SettingsViewModel>();
        setting.DataContext = vm;
        setting.ShowDialog(GlobalSingletonHelper.Service.GetRequiredService<MainWindow>());
    }

    [RelayCommand]
    private void About()
    {
        var view = GlobalSingletonHelper.Service.GetRequiredService<AboutWindow>();
        var vm = GlobalSingletonHelper.Service.GetRequiredService<AboutViewModel>();
        view.DataContext = vm;
        var owner = GlobalSingletonHelper.Service.GetRequiredService<MainWindow>();
        view.ShowDialog(owner);
    }

    [RelayCommand]
    private void EditDescription()
    {
        CanEditNodeDefine = false;
        _defDefineInfo.UpdateDefine(_searchDescNodeFullName, CurrentDescription);
    }

    [RelayCommand]
    private void OpenInitWindow()
    {
    }

    [RelayCommand]
    private void Exit()
    {
        Environment.Exit(0);
        // 后续增加
    }

    private void FlushChildListAfterAdd()
    {
        _childNodes = _childNodes.Except(_childViewNode.Children.Select(t => t.TagName));
        OnSearchChildTextChanged(string.Empty);
    }

    // 点击子项后也可以加载描述
    partial void OnChildSiderBarSelectItemChanged(string value)
    {
        var fullName = string.Empty;
        if (_childViewNode == DefNodeManager.RootNode)
            fullName = value;
        else
            fullName = $"{_childViewNode.GetFullName('.')}.{value}";
        UpdateDescription(fullName);
    }

    // 当任意一方内容发生变化, 就会触发重新解析, 每次重新解析都会保存一次
    partial void OnIsNodeEditingTabSelectedChanged(bool value)
    {
        if (value)
        {
            if (_xmlIsChanged && !string.IsNullOrWhiteSpace(NodeXmlContent))
            {
                _defManager.ConvertToDefNode(NodeXmlContent);
                _xmlIsChanged = false;
                _childViewNode = DefNodeManager.RootNode;
                //_ = UpdataChildList();
            }
        }
        else
        {
            var xmlContent = _defManager.ConvertToXml();
            _isInternalUpdate = true;
            NodeXmlContent = xmlContent;
            _isInternalUpdate = false;
        }
    }

    partial void OnNodeXmlContentChanged(string value)
    {
        if (!_isInternalUpdate) _xmlIsChanged = true;
    }

    private void UpdateDescription(string key)
    {
        if (string.IsNullOrEmpty(key)) return;

        var dec = _defDefineInfo.GetDefine(key);
        _searchDescNodeFullName = key;
        if (!string.IsNullOrEmpty(dec))
        {
            CurrentDescription = dec;
        }
        else
        {
            _log.LogDebug("未找到节点 {} 描述", key);
            CurrentDescription = "暂无此描述, 可能无此标签, 若为社区模组, 请加载模组的dll文件, 或在此处双击鼠标右键以编写自定义描述";
        }
    }
}