using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RimXmlEdit.Utils;
using RimXmlEdit.ViewModels;
using System;

namespace RimXmlEdit;

public partial class SimpleFileExplorer : UserControl
{
    public SimpleFileExplorer()
    {
        InitializeComponent();
        DataContext = GlobalSingletonHelper.Service.GetRequiredService<SimpleFileExplorerViewModel>();
    }

    private void FileListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is SimpleFileExplorerViewModel vm && FileListBox.SelectedItem is FileSystemItemViewModel selectedItem)
        {
            vm.OpenCommand.Execute(selectedItem);
        }
    }
}
