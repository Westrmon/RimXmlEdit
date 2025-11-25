using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Microsoft.Extensions.Options;
using RimXmlEdit.Core;
using RimXmlEdit.Core.Utils;
using System;
using System.IO;

namespace RimXmlEdit.ViewModels;

public partial class InitViewModel : ViewModelBase
{
    [ObservableProperty]
    private ViewModelBase? _contentViewModel;

    public SidebarViewModel Sidebar { get; }

    private readonly AppSettings _setting;

    public InitViewModel(
        SidebarViewModel sidebarViewModel,
        RecentProjectsViewModel recentProjectsViewModel,
        CreateNewProjectViewModel createNewProjectViewModel,
        IOptions<AppSettings> options)
    {
        //_logger = this.Log();
        Sidebar = sidebarViewModel;
        _setting = options.Value;
        // Link sidebar selection to content view model
        Sidebar.OnSelectionChanged = (selectedItem) =>
        {
            if (selectedItem?.NameKey == "Sidebar_RecentProjects")
            {
                ContentViewModel = recentProjectsViewModel;
            }
            else if (selectedItem?.NameKey == "Sidebar_CreateNewProject")
            {
                ContentViewModel = createNewProjectViewModel;
            }
        };
    }

    [RelayCommand]
    private void Close()
    {
        Environment.Exit(0);
    }

    public void OnLoaded()
    {
        if (string.IsNullOrEmpty(_setting.GamePath))
        {
            InitGamePath();
        }
        TempConfig.GamePath = _setting.GamePath;
        Directory.CreateDirectory(Path.Combine(TempConfig.AppPath, "Projects"));
    }

    private async void InitGamePath()
    {
        var diaglogView = new SelectRootPathView();
        var result = await DialogHost.Show(diaglogView, "InitDialogHost");
        if (result is string returnedText && !string.IsNullOrEmpty(returnedText))
        {
            TempConfig.GamePath = returnedText;
            _setting.GamePath = returnedText;
            _setting.SaveAppSettings();
        }
    }
}
