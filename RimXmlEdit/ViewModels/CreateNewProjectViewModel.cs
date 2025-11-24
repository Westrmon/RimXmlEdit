using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RimXmlEdit.Core;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Models;
using RimXmlEdit.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RimXmlEdit.ViewModels;

public record class ProjectTemplate(string Name);

public partial class CreateNewProjectViewModel : ViewModelBase
{
    private readonly ILogger _logger;

    [ObservableProperty]
    private string _author;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProjectCommand))]
    private string? _projectName;

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProjectCommand))]
    private string? _projectLocation;

    [ObservableProperty]
    private string? _projectDescription;

    [ObservableProperty]
    private string _modIconPath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateProjectCommand))]
    private ProjectTemplate? _selectedTemplate;

    public ObservableCollection<GameVersionCheckItem> GameVersions { get; } = new();

    public ObservableCollection<ModDependency> ModDependencies { get; } = new();

    public ObservableCollection<string> LoadAfterList { get; } = new();
    public ObservableCollection<string> LoadBeforeList { get; } = new();

    /// <summary>
    /// Gets the collection of available project templates.
    /// </summary>
    public ObservableCollection<ProjectTemplate> Templates { get; }

    private AppSettings _settings;

    public CreateNewProjectViewModel(IOptions<AppSettings> options)
    {
        _logger = this.Log();
        _settings = options.Value;
        string author = _settings.Author;
        if (!string.IsNullOrEmpty(author))
            Author = author;
        Templates = new ObservableCollection<ProjectTemplate>
            {
                new("普通 (Normal)")
            };
        InitializeData();
        SelectedTemplate = Templates.FirstOrDefault();
        this.PropertyChanged += OnAuthorOrProjectNameChanged;
        GameVersions.Add(new GameVersionCheckItem { DisplayName = "1.6", IsSelected = true });
        GameVersions.Add(new GameVersionCheckItem { DisplayName = "1.5" });
        GameVersions.Add(new GameVersionCheckItem { DisplayName = "1.4" });
    }

    private void InitializeData()
    {
        // 添加一个示例依赖
        ModDependencies.Add(new ModDependency
        {
            PackageId = "ludeon.rimworld",
            DisplayName = "Core",
        });

        // 添加一个示例加载顺序
        LoadAfterList.Add("brrainz.harmony");
    }

    private void OnAuthorOrProjectNameChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Author) || e.PropertyName == nameof(ProjectName))
        {
            OnPropertyChanged(nameof(PackageId));
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

    /// <summary>
    /// Determines if the project can be created based on the current form state.
    /// </summary>
    private bool CanCreateProject()
    {
        return !string.IsNullOrWhiteSpace(ProjectName) &&
               !string.IsNullOrWhiteSpace(ProjectLocation) &&
               SelectedTemplate is not null;
    }

    /// <summary>
    /// Command to create a new project.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreateProject))]
    private void CreateProject()
    {
        _logger.LogInformation("Attempting to create project '{}' at '{}'.", ProjectName, ProjectLocation);
        _settings.Author = Author.Split(',')[0].Replace(" ", "");
        var newItem = new RecentPorjectsItem
        {
            ProjectName = ProjectName,
            ProjectPath = Path.Combine(ProjectLocation, ProjectName)
        };
        _settings.RecentProjects.Add(newItem);
        _settings.CurrentProject = newItem;
        _settings.SaveAppSettings();
        Task.Run(() =>
        {
            var version = GameVersions.Where(x => x.IsSelected).Select(t => t.DisplayName);
            var dependenciesData = new List<Dictionary<string, string>>();
            if (ModDependencies.Count > 0)
            {
                foreach (var dependency in ModDependencies)
                {
                    var dependencyData = new Dictionary<string, string>
                    {
                        { "dep_packageId", dependency.PackageId },
                        { "dep_displayName", dependency.DisplayName },
                        { "dep_steamUrl", dependency.SteamWorkshopUrl }
                    };
                }
            }

            var userData = new Dictionary<string, object>
            {
                { "authors", Author },
                { "modName", ProjectName },
                { "modDependencies", ProjectDescription },
                { "modIconPath", ModIconPath },
                { "modLoadBefore", LoadBeforeList },
                { "modLoadAfter", LoadAfterList },
                { "packageId", PackageId },
                { "gameVersion", string.Join(',', version) },
                { "description", ProjectDescription },
            };

            InitProject.Init(userData, ProjectLocation);
        });
        TempConfig.ProjectPath = Path.Combine(ProjectLocation, ProjectName);

        WeakReferenceMessenger.Default.Send(new CloseWindowMessage { Sender = new WeakReference(this) });
    }

    [RelayCommand]
    private async Task BrowseIcon()
    {
        var storageService = GlobalSingletonHelper.StorageProvider;
        if (!storageService.CanOpen)
        {
            _logger.LogWarning("Cannot open folder picker.");
        }

        var files = await storageService.OpenFilePickerAsync(new FilePickerOpenOptions()
        {
            AllowMultiple = false,
            FileTypeFilter = new[] { FilePickerFileTypes.ImageAll }
        });
        if (files is not null && files.Any())
        {
            ModIconPath = files.First().Path.LocalPath;

            if (!ModIconPath.EndsWith(".png"))
            {
                File.Move(ModIconPath, ModIconPath.Split('.').Last() + ".png");
            }
        }
    }

    /// <summary>
    /// Command to open a folder browser to select the project location.
    /// </summary>
    [RelayCommand]
    private async Task BrowseLocationAsync()
    {
        var storageService = GlobalSingletonHelper.StorageProvider;
        if (!storageService.CanOpen)
        {
            _logger.LogWarning("Cannot open folder picker.");
        }

        var uri = await storageService.TryGetFolderFromPathAsync(Path.Combine(TempConfig.AppPath, "Projects"));

        FolderPickerOpenOptions options = new FolderPickerOpenOptions()
        {
            AllowMultiple = false,
            Title = "选择项目位置",
            SuggestedStartLocation = uri
        };

        var folders = await storageService.OpenFolderPickerAsync(options);
        if (folders is not null && folders.Any())
        {
            ProjectLocation = folders.First().Path.LocalPath;
        }
    }
}
