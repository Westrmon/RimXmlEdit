using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RimXmlEdit.Core;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Core.XmlOperator;
using RimXmlEdit.Models;
using RimXmlEdit.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static RimXmlEdit.Core.Utils.LoggerFactoryInstance;

namespace RimXmlEdit.ViewModels;

// 写的太傻逼了（‵□′）
public partial class SettingsViewModel : ViewModelBase
{
    private string _aboutXmlPath;
    private EditableString name;
    private EditableString author;
    private EditableString description;
    private EditableString packageId;
    private RXStruct rXStruct;
    private readonly ILogger _log;
    private readonly AppSettings _settings;
    public Action? CloseAction { get; set; }

    public SettingsViewModel(IOptions<AppSettings> appSettings)
    {
        _log = this.Log();
        _settings = appSettings.Value;
        _aboutXmlPath = Path.Combine(TempConfig.ProjectFolders["About"], "About.xml");
        InitializeNavigation();
        Initialize();
    }

    private async void Initialize()
    {
        try
        {
            var sf = await File.ReadAllTextAsync(_aboutXmlPath);
            rXStruct = XmlConverter.Deserialize(sf)!;
            foreach (var item in rXStruct.Defs)
            {
                switch (item.TagName)
                {
                    case nameof(name): name = new(item.Value); break;
                    case nameof(author): author = new(item.Value); break;
                    case nameof(description): description = new(item.Value); break;
                    case nameof(packageId): packageId = new(item.Value); break;
                    case "modDependencies":
                        foreach (var mod in item.Fields)
                        {
                            var dic = mod.Value as Dictionary<string, XmlFieldInfo>;
                            var modDependency = new ModDependency
                            {
                                PackageId = dic.TryGetValue("packageId", out var va1) ? va1.Value as string : string.Empty,
                                DisplayName = dic.TryGetValue("displayName", out var va2) ? va2.Value as string : string.Empty,
                                SteamWorkshopUrl = dic.TryGetValue("steamWorkshopUrl", out var va3) ? va3.Value as string : string.Empty,
                                DownloadUrl = dic.TryGetValue("downloadUrl", out var va4) ? va4.Value as string : string.Empty
                            };
                            ModDependencies.Add(modDependency);
                        }
                        break;

                    case "loadAfter":
                        foreach (var item2 in item.Fields)
                        {
                            var str = item2.Value as string;
                            LoadAfterList.Add(str);
                        }
                        break;

                    case "loadBefore":
                        foreach (var item2 in item.Fields)
                        {
                            var str = item2.Value as string;
                            LoadBeforeList.Add(str);
                        }
                        break;
                }
            }
            _settings.CurrentProject.DependentPaths.ForEach(x => DllDependencies.Add(new EditableString(x)));
            InitializeDynamicSettings();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Setup initialization failed");
        }
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    public ObservableCollection<SettingItemBase> GeneralSettings { get; } = new();

    public ObservableCollection<SettingItemBase> MetaDataSettings { get; } = new();

    public ObservableCollection<ModDependency> ModDependencies { get; } = new();

    public ObservableCollection<EditableString> DllDependencies { get; } = new();

    public ObservableCollection<string> LoadAfterList { get; } = new();
    public ObservableCollection<string> LoadBeforeList { get; } = new();

    [ObservableProperty]
    private NavigationItem _selectedNavigationItem;

    private void InitializeNavigation()
    {
        NavigationItems.Add(new NavigationItem { Title = "项目信息", TargetControlName = "Section_MetaData" });
        NavigationItems.Add(new NavigationItem { Title = "Mod 依赖", TargetControlName = "Section_Dependencies" });
        NavigationItems.Add(new NavigationItem { Title = "加载顺序", TargetControlName = "Section_LoadOrder" });
        NavigationItems.Add(new NavigationItem { Title = "通用设置", TargetControlName = "Section_General" });
    }

    private void InitializeDynamicSettings()
    {
        var logEnum = Enum.GetNames<LogLevelConfig>();
        // 通用设置
        GeneralSettings.Add(new EnumSettingItem { Label = "语言", Value = _settings.Language, EnumValues = ["简体中文", "English"] });
        GeneralSettings.Add(new BoolSettingItem { Label = "开启时自动加载dll依赖项", Value = _settings.AutoLoadDllDependencies });
        GeneralSettings.Add(new BoolSettingItem { Label = "节点自动展开", Value = _settings.AutoExpandNodes });
        GeneralSettings.Add(new TextSettingItem { Label = "值验证间隔(ms)", Description = "停止输入后一段时间验证, 间隔越短, 验证越频繁, 0为不验证", Value = new(_settings.ValueValidationInterval.ToString()), Watermark = "输入毫秒数" });
        GeneralSettings.Add(new BoolSettingItem { Label = "打开文件后自动验证所有值", Description = "工作区打开文件后立即验证所有已填项, 可能会影响打开时间", Value = _settings.AutoValidateValuesAfterOpen, IsUsed = false });
        GeneralSettings.Add(new NumberSettingItem { Label = "自动保存间隔(分钟)", Description = "自动保存到文件时间, 0为不自动保存", Value = _settings.AutoSaveInterval, Max = 60, Min = 0 });
        GeneralSettings.Add(new EnumSettingItem { Label = "日志记录级别", Value = _settings.FileLoggingLevel.ToString(), EnumValues = logEnum });
        GeneralSettings.Add(new EnumSettingItem { Label = "弹窗日志级别", Value = _settings.NotificationLoggingLevel.ToString(), EnumValues = logEnum });
        // 项目信息 (使用相同的模板逻辑)
        MetaDataSettings.Add(new TextSettingItem { Label = "项目名称", Value = name, Watermark = "输入项目名称" });
        MetaDataSettings.Add(new TextSettingItem { Label = "作者", Value = author, Watermark = "你的名字" });
        MetaDataSettings.Add(new TextSettingItem { Label = "Package ID", Value = packageId, Watermark = "唯一标识符" });
        MetaDataSettings.Add(new TextSettingItem { Label = "描述", Value = description, IsMultiline = true });
    }

    [RelayCommand]
    private void Close()
    {
        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void Save()
    {
        ApplyAbout();
        ApplySettingsToModel();
        _settings.SaveAppSettings();
        _log.LogNotify("The configuration was successfully saved");
        Close();
    }

    private void ApplyAbout()
    {
        rXStruct.Defs.Clear();
        rXStruct.Defs.Add(new DefInfo() { TagName = "name", Value = name.Value });
        rXStruct.Defs.Add(new DefInfo() { TagName = "author", Value = author.Value });
        rXStruct.Defs.Add(new DefInfo() { TagName = "description", Value = description.Value });
        rXStruct.Defs.Add(new DefInfo() { TagName = "packageId", Value = packageId.Value });

        var newValue = new List<XmlFieldInfo>(ModDependencies.Count);
        if (ModDependencies.Count > 0)
        {
            foreach (var mod in ModDependencies)
            {
                var dependencies = new Dictionary<string, XmlFieldInfo>();
                if (!string.IsNullOrEmpty(mod.PackageId))
                    dependencies["packageId"] = new XmlFieldInfo { Name = "packageId", Value = mod.PackageId };
                if (!string.IsNullOrEmpty(mod.DisplayName))
                    dependencies["displayName"] = new XmlFieldInfo { Name = "displayName", Value = mod.DisplayName };
                if (!string.IsNullOrEmpty(mod.SteamWorkshopUrl))
                    dependencies["steamWorkshopUrl"] = new XmlFieldInfo { Name = "steamWorkshopUrl", Value = mod.SteamWorkshopUrl };
                if (!string.IsNullOrEmpty(mod.DownloadUrl))
                    dependencies["downloadUrl"] = new XmlFieldInfo { Name = "downloadUrl", Value = mod.DownloadUrl };
                newValue.Add(new XmlFieldInfo() { Name = "li", Value = dependencies });
            }
            rXStruct.Defs.Add(new DefInfo() { TagName = "modDependencies", Fields = newValue });
        }
        if (LoadAfterList.Count > 0)
        {
            var loadAfter = new List<XmlFieldInfo>();
            foreach (var item2 in LoadAfterList)
            {
                loadAfter.Add(new XmlFieldInfo { Name = "li", Value = item2 });
            }
            rXStruct.Defs.Add(new DefInfo() { TagName = "loadAfter", Fields = loadAfter });
        }
        if (LoadBeforeList.Count > 0)
        {
            var loadBefore = new List<XmlFieldInfo>();
            foreach (var item2 in LoadBeforeList)
            {
                loadBefore.Add(new XmlFieldInfo { Name = "li", Value = item2 });
            }
            rXStruct.Defs.Add(new DefInfo() { TagName = "loadBefore", Fields = loadBefore });
        }
        var content = XmlConverter.SerializeAbout(rXStruct);
        File.WriteAllTextAsync(_aboutXmlPath, content);
    }

    private void ApplySettingsToModel()
    {
        foreach (var item in GeneralSettings)
        {
            switch (item.Label)
            {
                case "开启时自动加载dll依赖项":
                    if (item is BoolSettingItem autoLoad)
                        _settings.AutoLoadDllDependencies = autoLoad.Value;
                    break;

                case "节点自动展开":
                    if (item is BoolSettingItem autoExpand)
                        _settings.AutoExpandNodes = autoExpand.Value;
                    break;

                case "值验证间隔(ms)":
                    if (item is TextSettingItem valInterval && int.TryParse(valInterval.Value.Value, out int v))
                        _settings.ValueValidationInterval = v;
                    break;

                case "打开文件后自动验证所有值":
                    if (item is BoolSettingItem autoValidate)
                        _settings.AutoValidateValuesAfterOpen = autoValidate.Value;
                    break;

                case "自动保存间隔(分钟)":
                    if (item is NumberSettingItem autoSave)
                        _settings.AutoSaveInterval = autoSave.Value;
                    break;

                case "日志记录级别":
                    if (item is EnumSettingItem logLevel)
                        _settings.FileLoggingLevel = Enum.Parse<LogLevelConfig>(logLevel.Value);
                    break;

                case "弹窗日志级别":
                    if (item is EnumSettingItem logLevel2)
                    {
                        _settings.NotificationLoggingLevel = Enum.Parse<LogLevelConfig>(logLevel2.Value);
                    }
                    break;

                case "语言":
                    if (item is EnumSettingItem language)
                        _settings.Language = language.Value?.ToString() ?? "zh-CN";
                    break;
            }
        }
        _settings.CurrentProject.DependentPaths.Clear();
        foreach (var item in DllDependencies)
        {
            if (!string.IsNullOrEmpty(item.Value))
            {
                _settings.CurrentProject.DependentPaths.Add(item.Value);
            }
        }
    }

    [RelayCommand]
    private void AddDependency()
    {
        ModDependencies.Add(new ModDependency());
    }

    [RelayCommand]
    private void RemoveDependency(ModDependency dependency)
    {
        if (dependency != null)
        {
            ModDependencies.Remove(dependency);
        }
    }

    [RelayCommand]
    private void AddLoadAfter()
    {
        LoadAfterList.Add("");
    }

    [RelayCommand]
    private void RemoveLoadAfter(string packageId)
    {
        if (packageId != null)
        {
            LoadAfterList.Remove(packageId);
        }
    }

    [RelayCommand]
    private void AddLoadBefore()
    {
        LoadBeforeList.Add("");
    }

    [RelayCommand]
    private void RemoveLoadBefore(string packageId)
    {
        if (packageId != null)
        {
            LoadBeforeList.Remove(packageId);
        }
    }

    [RelayCommand]
    private void AddDllDependency()
    {
        DllDependencies.Add(new EditableString(string.Empty));
    }

    [RelayCommand]
    private void RemoveDllDependency(EditableString path)
    {
        if (packageId != null)
        {
            DllDependencies.Remove(path);
        }
    }

    [RelayCommand] // 需要放在一个地方
    private async Task ImportDll(EditableString pathd)
    {
        var file = await GlobalSingletonHelper.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new FilePickerFileType("dll Files"){ Patterns = new[] { "*.dll" } },
            },
            Title = "Import dll File"
        });
        if (file is null) return;
        pathd.Value = file.FirstOrDefault()?.Path.LocalPath;
    }
}
