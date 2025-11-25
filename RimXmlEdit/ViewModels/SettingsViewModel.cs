using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RimXmlEdit.Core;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Models;
using RimXmlEdit.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RimXmlEdit.Core.Utils.LoggerFactoryInstance;

namespace RimXmlEdit.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private RXStruct _rXStruct;
    private readonly string _aboutXmlPath;
    private readonly ILogger _log;
    private readonly AppSettings _settings;
    private readonly AboutMetadata _tempMetadata = new();
    public Action? CloseAction { get; set; }

    public ObservableCollection<NavigationItem> NavigationItems { get; } = new();

    public ObservableCollection<SettingItemBase> GeneralSettings { get; } = new();

    public ObservableCollection<SettingItemBase> MetaDataSettings { get; } = new();

    public ObservableCollection<ModDependency> ModDependencies { get; } = new();

    public ObservableCollection<EditableString> DllDependencies { get; } = new();

    public ObservableCollection<EditableString> LoadAfterList { get; } = new();
    public ObservableCollection<EditableString> LoadBeforeList { get; } = new();

    [ObservableProperty]
    private NavigationItem _selectedNavigationItem;

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
            await LoadAboutXmlAsync();
            LoadDllDependencies();
            InitializeDynamicSettings();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Settings initialization failed");
        }
    }

    private void InitializeNavigation()
    {
        NavigationItems.Add(new NavigationItem { Title = "项目信息", TargetControlName = "Section_MetaData" });
        NavigationItems.Add(new NavigationItem { Title = "Mod 依赖", TargetControlName = "Section_Dependencies" });
        NavigationItems.Add(new NavigationItem { Title = "加载顺序", TargetControlName = "Section_LoadOrder" });
        NavigationItems.Add(new NavigationItem { Title = "通用设置", TargetControlName = "Section_General" });
    }

    private void InitializeDynamicSettings()
    {
        var logLevels = Enum.GetNames<LogLevelConfig>();

        GeneralSettings.Add(new EnumSettingItem(
            "语言",
            _settings.Language,
            val => _settings.Language = val,
            ["简体中文", "English"]
        ));

        GeneralSettings.Add(new BoolSettingItem(
            "开启时自动加载dll依赖项",
            _settings.AutoLoadDllDependencies,
            val => _settings.AutoLoadDllDependencies = val
        ));

        GeneralSettings.Add(new BoolSettingItem(
            "节点自动展开",
            _settings.AutoExpandNodes,
            val => _settings.AutoExpandNodes = val
        ));

        GeneralSettings.Add(new TextSettingItem(
            "值验证间隔(ms)",
            _settings.ValueValidationInterval.ToString(),
            val =>
            {
                if (int.TryParse(val, out int v))
                    _settings.ValueValidationInterval = v;
            })
        {
            Description = "停止输入后一段时间验证, 间隔越短, 验证越频繁, 0为不验证",
            Watermark = "输入毫秒数"
        });

        GeneralSettings.Add(new BoolSettingItem(
            "打开文件后自动验证所有值",
            _settings.AutoValidateValuesAfterOpen,
            val => _settings.AutoValidateValuesAfterOpen = val)
        {
            Description = "工作区打开文件后立即验证所有已填项, 可能会影响打开时间",
            IsUsed = false
        });

        GeneralSettings.Add(new NumberSettingItem(
            "自动保存间隔(分钟)",
            _settings.AutoSaveInterval,
            val => _settings.AutoSaveInterval = val)
        {
            Description = "自动保存到文件时间, 0为不自动保存",
            Max = 60,
            Min = 0
        });

        GeneralSettings.Add(new EnumSettingItem(
            "日志记录级别",
            _settings.FileLoggingLevel.ToString(),
            val => _settings.FileLoggingLevel = Enum.Parse<LogLevelConfig>(val),
            logLevels
        ));

        GeneralSettings.Add(new EnumSettingItem(
            "弹窗日志级别",
            _settings.NotificationLoggingLevel.ToString(),
            val => _settings.NotificationLoggingLevel = Enum.Parse<LogLevelConfig>(val),
            logLevels
        ));

        MetaDataSettings.Add(new TextSettingItem(
            "项目名称",
            _tempMetadata.Name,
            val => _tempMetadata.Name = val)
        {
            Watermark = "输入项目名称"
        });

        MetaDataSettings.Add(new TextSettingItem(
            "作者",
            _tempMetadata.Author,
            val => _tempMetadata.Author = val
        ));

        MetaDataSettings.Add(new TextSettingItem(
            "Package ID",
            _tempMetadata.PackageId,
            val => _tempMetadata.PackageId = val)
        {
            Watermark = "唯一标识符"
        });

        MetaDataSettings.Add(new TextSettingItem(
            "描述",
            _tempMetadata.Description,
            val => _tempMetadata.Description = val)
        {
            IsMultiline = true
        });
        MetaDataSettings.Add(new TextSettingItem(
            "支持版本",
            _tempMetadata.Version,
            val => _tempMetadata.Version = val)
        {
            Description = "请输入支持的游戏版本,并使用逗号分隔"
        });
    }

    private async Task LoadAboutXmlAsync()
    {
        if (!File.Exists(_aboutXmlPath)) return;
        var sf = await File.ReadAllTextAsync(_aboutXmlPath);
        _rXStruct = XmlConverter.Deserialize(sf)!;
        foreach (var item in _rXStruct.Defs)
        {
            switch (item.TagName)
            {
                case "name": _tempMetadata.Name = item.Value; break;
                case "author": _tempMetadata.Author = item.Value; break;
                case "description": _tempMetadata.Description = item.Value; break;
                case "packageId": _tempMetadata.PackageId = item.Value; break;
                case "modDependencies":
                    ParseModDependencies(item);
                    break;

                case "loadAfter":
                    ParseList(item, LoadAfterList);
                    break;

                case "loadBefore":
                    ParseList(item, LoadBeforeList);
                    break;

                case "supportedVersions":
                    _tempMetadata.Version = CombinList(item);
                    break;
            }
        }
    }

    private void LoadDllDependencies()
    {
        DllDependencies.Clear();
        _settings.CurrentProject.DependentPaths.ForEach(x => DllDependencies.Add(new EditableString(x)));
    }

    private void ParseModDependencies(DefInfo item)
    {
        foreach (var mod in item.Fields)
        {
            if (mod.Value is Dictionary<string, XmlFieldInfo> dic)
            {
                ModDependencies.Add(new ModDependency
                {
                    PackageId = GetXmlStr(dic, "packageId"),
                    DisplayName = GetXmlStr(dic, "displayName"),
                    SteamWorkshopUrl = GetXmlStr(dic, "steamWorkshopUrl"),
                    DownloadUrl = GetXmlStr(dic, "downloadUrl")
                });
            }
        }
    }

    private void ParseList(DefInfo item, ObservableCollection<EditableString> list)
    {
        foreach (var field in item.Fields)
        {
            if (field.Value is string value)
                list.Add(new EditableString(value));
        }
    }

    private string CombinList(DefInfo item)
    {
        if (item.Fields.Count == 0)
            return item.Value;
        var sb = new StringBuilder(item.Fields.Count * 2 - 1);
        foreach (var field in item.Fields)
        {
            if (field.Value is string value)
            {
                sb.Append(value);
                sb.Append(',');
            }
        }
        return sb.Remove(sb.Length - 1, 1).ToString();
    }

    private string GetXmlStr(Dictionary<string, XmlFieldInfo> dic, string key)
       => dic.TryGetValue(key, out var v) && v.Value is string s ? s : string.Empty;

    [RelayCommand]
    private void Close()
    {
        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            foreach (var item in GeneralSettings)
                item.Commit();

            foreach (var item in MetaDataSettings)
                item.Commit();

            _settings.CurrentProject.DependentPaths.Clear();
            _settings.CurrentProject.DependentPaths.AddRange(DllDependencies
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrEmpty(x)));
            _settings.SaveAppSettings();
            SaveAboutXml();
            _log.LogNotify("The configuration was successfully saved");
            Close();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to save settings");
        }
    }

    private void SaveAboutXml()
    {
        if (_rXStruct == null) return;

        UpdateOrAddSingleValue("name", _tempMetadata.Name);
        UpdateOrAddSingleValue("author", _tempMetadata.Author);
        UpdateOrAddSingleValue("description", _tempMetadata.Description);
        UpdateOrAddSingleValue("packageId", _tempMetadata.PackageId);
        UpdateOrAddSingleValue("supportedVersions", _tempMetadata.Version);
        var modFields = new List<XmlFieldInfo>();
        foreach (var mod in ModDependencies)
        {
            var props = new Dictionary<string, XmlFieldInfo>();
            AddIfNotEmpty(props, "packageId", mod.PackageId);
            AddIfNotEmpty(props, "displayName", mod.DisplayName);
            AddIfNotEmpty(props, "steamWorkshopUrl", mod.SteamWorkshopUrl);
            AddIfNotEmpty(props, "downloadUrl", mod.DownloadUrl);
            if (props.Count > 0)
            {
                modFields.Add(new XmlFieldInfo { Name = "li", Value = props });
            }
        }

        UpdateOrAddListValue("modDependencies", modFields);
        var loadAfterFields = LoadAfterList
            .Where(s => !string.IsNullOrWhiteSpace(s.Value))
            .Select(s => new XmlFieldInfo { Name = "li", Value = s.Value })
            .ToList();
        UpdateOrAddListValue("loadAfter", loadAfterFields);

        var loadBeforeFields = LoadBeforeList
            .Where(s => !string.IsNullOrWhiteSpace(s.Value))
            .Select(s => new XmlFieldInfo { Name = "li", Value = s.Value })
            .ToList();
        UpdateOrAddListValue("loadBefore", loadBeforeFields);
        var content = XmlConverter.SerializeAbout(_rXStruct);
        File.WriteAllText(_aboutXmlPath, content);
    }

    private void UpdateOrAddSingleValue(string tagName, string value)
    {
        var def = _rXStruct.Defs.FirstOrDefault(x => x.TagName == tagName);
        if (def != null)
            def.Value = value;
        else if (!string.IsNullOrEmpty(value))
            _rXStruct.Defs.Add(new DefInfo { TagName = tagName, Value = value });
    }

    private void UpdateOrAddListValue(string tagName, List<XmlFieldInfo> newFields)
    {
        var def = _rXStruct.Defs.FirstOrDefault(x => x.TagName == tagName);

        if (newFields == null || newFields.Count == 0)
        {
            if (def != null)
                _rXStruct.Defs.Remove(def);
        }
        else
        {
            if (def != null)
                def.Fields = newFields;
            else
                _rXStruct.Defs.Add(new DefInfo { TagName = tagName, Fields = newFields });
        }
    }

    private void AddIfNotEmpty(Dictionary<string, XmlFieldInfo> dict, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            dict[key] = new XmlFieldInfo { Name = key, Value = value };
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
        LoadAfterList.Add(new EditableString(string.Empty));
    }

    [RelayCommand]
    private void RemoveLoadAfter(EditableString packageId)
    {
        if (packageId != null)
        {
            LoadAfterList.Remove(packageId);
        }
    }

    [RelayCommand]
    private void AddLoadBefore()
    {
        LoadBeforeList.Add(new EditableString(string.Empty));
    }

    [RelayCommand]
    private void RemoveLoadBefore(EditableString packageId)
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
        if (path != null)
        {
            DllDependencies.Remove(path);
        }
    }

    [RelayCommand]
    private async Task ImportDll(EditableString pathd)
    {
        var files = await GlobalSingletonHelper.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[] { new FilePickerFileType("dll Files") { Patterns = new[] { "*.dll" } } },
            Title = "Import dll File List"
        });

        if (files?.Count > 0)
        {
            pathd.Value = files[0].Path.LocalPath;
        }
    }

    private class AboutMetadata
    {
        public string Name { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string PackageId { get; set; } = "";
        public string Version { get; set; } = "";
    }
}
