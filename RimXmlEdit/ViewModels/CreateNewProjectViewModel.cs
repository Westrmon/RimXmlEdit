using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RimXmlEdit.Core;
using RimXmlEdit.Core.Entries;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Models;
using RimXmlEdit.Utils;

namespace RimXmlEdit.ViewModels;

public record class ProjectTemplate(string Name);

public partial class CreateNewProjectViewModel : ViewModelBase
{
    private readonly ILogger _logger;

    [ObservableProperty] private string _author;

    [ObservableProperty] private string _modIconPath;

    [ObservableProperty] private string? _projectDescription;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(CreateProjectCommand))]
    private string? _projectLocation;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(CreateProjectCommand))]
    private string? _projectName;

    [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(CreateProjectCommand))]
    private ProjectTemplate? _selectedTemplate;

    private readonly AppSettings _settings;

    public CreateNewProjectViewModel(IOptions<AppSettings> options)
    {
        _logger = this.Log();
        _settings = options.Value;
        var author = _settings.Author;
        if (!string.IsNullOrEmpty(author))
            Author = author;
        Templates = new ObservableCollection<ProjectTemplate>
        {
            new("普通 (Normal)")
        };
        InitializeData();
        SelectedTemplate = Templates.FirstOrDefault();
        PropertyChanged += OnAuthorOrProjectNameChanged;
        GameVersions.Add(new GameVersionCheckItem { DisplayName = "1.6", IsSelected = true });
        GameVersions.Add(new GameVersionCheckItem { DisplayName = "1.5" });
        GameVersions.Add(new GameVersionCheckItem { DisplayName = "1.4" });
    }

    public string PackageId
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Author) || string.IsNullOrWhiteSpace(ProjectName))
                return "请填写作者和项目名";
            var name = Regex.Match(Author.Split(',')[0], @"[a-zA-Z][a-zA-Z]*(?=[^a-zA-Z]|$)").Value;
            return $"{name}.{ProjectName}".Replace(" ", "");
        }
    }

    public ObservableCollection<GameVersionCheckItem> GameVersions { get; } = new();

    public ObservableCollection<ModDependency> ModDependencies { get; } = new();

    public ObservableCollection<EditableString> LoadAfterList { get; } = new();
    public ObservableCollection<EditableString> LoadBeforeList { get; } = new();

    /// <summary>
    ///     Gets the collection of available project templates.
    /// </summary>
    public ObservableCollection<ProjectTemplate> Templates { get; }

    private void InitializeData()
    {
        // 添加一个示例依赖
        ModDependencies.Add(new ModDependency
        {
            PackageId = "ludeon.rimworld",
            DisplayName = "Core"
        });

        // 添加一个示例加载顺序
        LoadAfterList.Add(new EditableString("brrainz.harmony"));
    }

    private void OnAuthorOrProjectNameChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Author) || e.PropertyName == nameof(ProjectName))
            OnPropertyChanged(nameof(PackageId));
    }

    [RelayCommand]
    private void AddDependency()
    {
        ModDependencies.Add(new ModDependency());
    }

    [RelayCommand]
    private void RemoveDependency(ModDependency dependency)
    {
        if (dependency != null) ModDependencies.Remove(dependency);
    }

    [RelayCommand]
    private void AddLoadAfter()
    {
        LoadAfterList.Add(new EditableString(""));
    }

    [RelayCommand]
    private void RemoveLoadAfter(EditableString packageId)
    {
        if (packageId != null) LoadAfterList.Remove(packageId);
    }

    [RelayCommand]
    private void AddLoadBefore()
    {
        LoadBeforeList.Add(new EditableString(""));
    }

    [RelayCommand]
    private void RemoveLoadBefore(EditableString packageId)
    {
        if (packageId != null) LoadBeforeList.Remove(packageId);
    }

    /// <summary>
    ///     Determines if the project can be created based on the current form state.
    /// </summary>
    private bool CanCreateProject()
    {
        return !string.IsNullOrWhiteSpace(ProjectName) &&
               !string.IsNullOrWhiteSpace(ProjectLocation) &&
               SelectedTemplate is not null;
    }

    /// <summary>
    ///     Command to create a new project.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreateProject))]
    private void CreateProject()
    {
        if (ProjectName == null) return;
        var displayName = ProjectName;
        ProjectName = ProjectName.Replace(' ', '_');
        if (!ProjectLocation.TrimEnd(Path.DirectorySeparatorChar).EndsWith(ProjectName, StringComparison.OrdinalIgnoreCase))
            ProjectLocation = Path.Combine(ProjectLocation, ProjectName);

        _logger.LogInformation("Attempting to create project '{name}' at '{location}'.", ProjectName, ProjectLocation);
        _settings.Author = Author.Split(',')[0].Replace(" ", "");
        
        var newItem = new RecentPorjectsItem
        {
            ProjectName = displayName,
            ProjectPath = ProjectLocation
        };
        _settings.RecentProjects.Add(newItem);
        _settings.CurrentProject = newItem;
        _settings.SaveAppSettings();
        var userData = new Dictionary<string, object>
        {
            { "modName", ProjectName },
            { "gameVersion", GameVersions.Where(e => e.IsSelected).Select(t => t.DisplayName) }
        };
        Task.Run(() =>
        {
            var projectPath = InitProject.Init(userData, ProjectLocation);
            CreateAboutXml(Path.Combine(projectPath, "About", "About.xml"));
        });
        TempConfig.ProjectPath = ProjectLocation;
        WeakReferenceMessenger.Default.Send(new CloseWindowMessage { Sender = new WeakReference(this) });
    }

    [RelayCommand]
    private async Task BrowseIcon()
    {
        var storageService = GlobalSingletonHelper.StorageProvider;
        if (!storageService.CanOpen) _logger.LogWarning("Cannot open folder picker.");

        var files = await storageService.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });
        if (files is not null && files.Any())
        {
            ModIconPath = files.First().Path.LocalPath;

            if (!ModIconPath.EndsWith(".png")) File.Move(ModIconPath, ModIconPath.Split('.').Last() + ".png");
        }
    }

    /// <summary>
    ///     Command to open a folder browser to select the project location.
    /// </summary>
    [RelayCommand]
    private async Task BrowseLocationAsync()
    {
        var storageService = GlobalSingletonHelper.StorageProvider;
        if (!storageService.CanOpen) _logger.LogWarning("Cannot open folder picker.");

        var uri = await storageService.TryGetFolderFromPathAsync(Path.Combine(TempConfig.AppPath, "Projects"));

        var options = new FolderPickerOpenOptions
        {
            AllowMultiple = false,
            Title = "选择项目位置",
            SuggestedStartLocation = uri
        };

        var folders = await storageService.OpenFolderPickerAsync(options);
        if (folders is not null && folders.Any()) ProjectLocation = folders.First().Path.LocalPath;
    }

    private void CreateAboutXml(string filePath)
    {
        var rXStruct = new RXStruct();
        rXStruct.Defs.Add(new DefInfo { TagName = "name", Value = ProjectName });
        rXStruct.Defs.Add(new DefInfo { TagName = "author", Value = Author });
        rXStruct.Defs.Add(new DefInfo { TagName = "packageId", Value = PackageId });
        rXStruct.Defs.Add(new DefInfo
            { TagName = "description", Value = ProjectDescription ?? "No description provided." });
        if (!string.IsNullOrEmpty(ModIconPath))
            rXStruct.Defs.Add(new DefInfo { TagName = "iconPath", Value = "ModIcon.png" });
        var versions = GameVersions.Where(x => x.IsSelected).Select(x => x.DisplayName).ToList();
        if (versions.Count != 0)
        {
            var versionFields = versions.Select(v => new XmlFieldInfo { Name = "li", Value = v }).ToList();
            rXStruct.Defs.Add(new DefInfo { TagName = "supportedVersions", Fields = versionFields });
        }

        if (ModDependencies.Any())
        {
            var modFields = new List<XmlFieldInfo>();
            foreach (var mod in ModDependencies)
            {
                var props = new Dictionary<string, XmlFieldInfo>();
                AddIfNotEmpty(props, "packageId", mod.PackageId);
                AddIfNotEmpty(props, "displayName", mod.DisplayName);
                AddIfNotEmpty(props, "steamWorkshopUrl", mod.SteamWorkshopUrl);
                AddIfNotEmpty(props, "downloadUrl", mod.DownloadUrl);

                if (props.Count > 0)
                    modFields.Add(new XmlFieldInfo { Name = "li", Value = props });
            }

            if (modFields.Count > 0)
                rXStruct.Defs.Add(new DefInfo { TagName = "modDependencies", Fields = modFields });
        }

        AddListToDefs(rXStruct, "loadAfter", LoadAfterList);
        AddListToDefs(rXStruct, "loadBefore", LoadBeforeList);
        var content = XmlConverter.SerializeAbout(rXStruct);
        var dir = Path.GetDirectoryName(filePath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);
        File.WriteAllText(filePath, content);
    }

    private void AddListToDefs(RXStruct rXStruct, string tagName, IEnumerable<EditableString> list)
    {
        var validItems = list.Where(s => !string.IsNullOrWhiteSpace(s.Value)).ToList();
        if (!validItems.Any()) return;

        var fields = validItems.Select(x => new XmlFieldInfo { Name = "li", Value = x.Value }).ToList();
        rXStruct.Defs.Add(new DefInfo { TagName = tagName, Fields = fields });
    }

    private void AddIfNotEmpty(Dictionary<string, XmlFieldInfo> dict, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            dict[key] = new XmlFieldInfo { Name = key, Value = value };
    }
}