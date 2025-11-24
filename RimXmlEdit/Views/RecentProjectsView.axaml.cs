using Avalonia.Controls;
using Avalonia.Input;
using Microsoft.Extensions.DependencyInjection;
using RimXmlEdit.Utils;
using RimXmlEdit.ViewModels;
using System;

namespace RimXmlEdit;

public partial class RecentProjectsView : UserControl
{
    public static event EventHandler<TappedEventArgs>? OnDoubleTapped;

    public RecentProjectsView()
    {
        InitializeComponent();
        DataContext = GlobalSingletonHelper.Service.GetRequiredService<RecentProjectsViewModel>();
    }

    private void ListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        OnDoubleTapped?.Invoke(sender, e);
    }
}
