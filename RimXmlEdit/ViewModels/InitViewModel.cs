using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using RimXmlEdit.Core;
using RimXmlEdit.Core.Utils;
using System;
using System.Linq;

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
        Sidebar.IsInitGamePath = _setting.GamePath != string.Empty;
        if (Sidebar.IsInitGamePath)
            TempConfig.GamePath = _setting.GamePath;
        // Set initial view
        Sidebar.SelectedItem = Sidebar.Items.First();
    }

    [RelayCommand]
    private void Close()
    {
        Environment.Exit(0);
    }
}
