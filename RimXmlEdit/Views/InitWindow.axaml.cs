using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using CommunityToolkit.Mvvm.Messaging;
using DialogHostAvalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Utils;
using RimXmlEdit.ViewModels;
using RimXmlEdit.Views;
using System;

namespace RimXmlEdit;

public partial class InitWindow : Window
{
    public InitWindow()
    {
        InitializeComponent();
        WeakReferenceMessenger.Default.Register<CloseWindowMessage>(this, (_, m) =>
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = GlobalSingletonHelper.Service.GetRequiredService<MainWindow>();
                window.DataContext = GlobalSingletonHelper.Service.GetRequiredService<MainViewModel>();
                desktop.MainWindow = window;
                var topLevel = GetTopLevel(window);
                GlobalSingletonHelper.StorageProvider = topLevel.StorageProvider;
                GlobalSingletonHelper.Launcher = topLevel.Launcher;
                window.Show();
            }
            Close();
        });

        Sidebar.DataContext = GlobalSingletonHelper.Service.GetRequiredService<SidebarViewModel>();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetPosition(this).Y < 30)
            this.BeginMoveDrag(e);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        (DataContext as InitViewModel)?.OnLoaded();
    }
}
