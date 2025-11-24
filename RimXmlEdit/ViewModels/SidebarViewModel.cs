using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.Options;
using RimXmlEdit.Core;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Models;
using RimXmlEdit.Utils;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RimXmlEdit.ViewModels;

public partial class SidebarViewModel : ViewModelBase
{
    /// <summary>
    /// Action triggered when the selected navigation item changes.
    /// </summary>
    public Action<SidebarItem?>? OnSelectionChanged { get; set; }

    public ObservableCollection<SidebarItem> Items { get; }

    private readonly AppSettings _setting;

    [ObservableProperty]
    public bool _isInitGamePath;

    [ObservableProperty]
    private SidebarItem? _selectedItem;

    public string Version => $"v {TempConfig.AppVersion}";

    /// <summary>
    /// Initializes a new instance of the <see cref="SidebarViewModel" /> class.
    /// </summary>
    public SidebarViewModel(IOptions<AppSettings> options)
    {
        Items = new ObservableCollection<SidebarItem>
            {
                new("Sidebar_RecentProjects"),
                new("Sidebar_CreateNewProject"),
                new("Sidebar_OpenProjectFromFolder")
            };
        _setting = options.Value;
    }

    /// <summary>
    /// Called when the SelectedItem property changes.
    /// </summary>
    /// <param name="value"> The new selected item. </param>
    partial void OnSelectedItemChanged(SidebarItem? value)
    {
        if (value?.NameKey == "Sidebar_OpenProjectFromFolder")
        {
            OpenProjectFromFolder();
        }
        OnSelectionChanged?.Invoke(value);
    }

    private async void OpenProjectFromFolder()
    {
        var path = await SelectFolderAsync("Select game root folder");
        if (string.IsNullOrEmpty(path))
            return;
        if (!_setting.RecentProjects.Any(p => p.ProjectPath == path))
        {
            var newItem = new RecentPorjectsItem
            {
                ProjectName = path.Split(Path.DirectorySeparatorChar, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Last(),
                ProjectPath = path
            };
            _setting.RecentProjects.Add(newItem);
            _setting.CurrentProject = newItem;
            _setting.SaveAppSettings();
        }
        TempConfig.ProjectPath = path;
        WeakReferenceMessenger.Default.Send(new CloseWindowMessage { Sender = new WeakReference(this) });
    }

    [RelayCommand]
    private async Task SelectGameRootPathAsync()
    {
        var path = await SelectFolderAsync("Select game root folder");
        if (string.IsNullOrEmpty(path))
            return;
        _setting.GamePath = path;
        _setting.SaveAppSettings();
        TempConfig.GamePath = path;
        IsInitGamePath = true;
    }

    private async Task<string> SelectFolderAsync(string title)
    {
        var storageService = GlobalSingletonHelper.StorageProvider;
        if (!storageService.CanOpen)
        {
            throw new InvalidOperationException("Cannot open folder picker.");
        }

        var uri = await storageService.TryGetFolderFromPathAsync(new Uri(TempConfig.AppPath));

        FolderPickerOpenOptions options = new FolderPickerOpenOptions()
        {
            AllowMultiple = false,
            Title = title,
            SuggestedStartLocation = uri
        };

        var folders = await storageService.OpenFolderPickerAsync(options);
        if (folders is not null && folders.Any())
        {
            return folders[0].Path.LocalPath;
        }
        return string.Empty;
    }
}
