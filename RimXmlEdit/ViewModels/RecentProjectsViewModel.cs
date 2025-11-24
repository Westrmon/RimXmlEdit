using Avalonia.Input;
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
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;

namespace RimXmlEdit.ViewModels;

public partial class RecentProjectsViewModel : ViewModelBase
{
    private readonly AppSettings appSettings;
    private readonly ILogger _logger;

    /// <summary>
    /// The complete list of recent projects.
    /// </summary>
    public ObservableCollection<ModProject> Projects { get; } = [];

    /// <summary>
    /// The filtered list of projects displayed to the user.
    /// </summary>
    public ObservableCollection<ModProject> FilteredProjects { get; }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ModProject? _selectedProject;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecentProjectsViewModel" /> class.
    /// </summary>
    public RecentProjectsViewModel(IOptions<AppSettings> settings)
    {
        _logger = this.Log();
        RecentProjectsView.OnDoubleTapped += OnDoubleTapped;
        appSettings = settings.Value;
        appSettings.RecentProjects.ForEach(p => Projects.Add(new ModProject(p.ProjectName, p.ProjectPath)));

        FilteredProjects = new ObservableCollection<ModProject>(Projects);
        _logger.LogInformation("RecentProjectsViewModel initialized with {Count} projects.", Projects.Count);
    }

    /// <summary>
    /// Called automatically when the SearchText property changes. This method filters the project list.
    /// </summary>
    /// <param name="value"> The new search text. </param>
    partial void OnSearchTextChanged(string value)
    {
        FilteredProjects.Clear();

        if (string.IsNullOrWhiteSpace(value))
        {
            foreach (var project in Projects)
            {
                FilteredProjects.Add(project);
            }
        }
        else
        {
            var filtered = Projects.Where(p => p.Name.Contains(value, System.StringComparison.OrdinalIgnoreCase));
            foreach (var project in filtered)
            {
                FilteredProjects.Add(project);
            }
        }

        _logger.LogInformation("Filtered project list with search term '{SearchTerm}'. Found {Count} results.", value, FilteredProjects.Count);
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs args)
    {
        if (SelectedProject is not null)
        {
            InitProject.Load();
            appSettings.CurrentProject = appSettings.RecentProjects.First(t => t.ProjectName == SelectedProject.Name);
            TempConfig.ProjectPath = SelectedProject.Path;
            WeakReferenceMessenger.Default.Send(new CloseWindowMessage { Sender = new WeakReference(this) });
        }
    }

    [RelayCommand]
    private void Delete(ModProject project)
    {
        Projects.Remove(project);
        OnSearchTextChanged("");
        var item = appSettings.RecentProjects.First(p => p.ProjectName == project.Name);
        appSettings.RecentProjects.Remove(item);
        appSettings.SaveAppSettings();
    }
}
