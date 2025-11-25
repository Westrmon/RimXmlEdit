using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using RimXmlEdit.Utils;
using System.Collections.Generic;
using System.Linq;

namespace RimXmlEdit;

public partial class SelectRootPathView : UserControl
{
    public SelectRootPathView()
    {
        InitializeComponent();
    }

    private async void SelectButton_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folder = await GlobalSingletonHelper.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
        {
            AllowMultiple = false,
            Title = "Select your game root path"
        });
        if (folder is null) return;
        string? path = folder.FirstOrDefault()?.Path.LocalPath;
        if (string.IsNullOrEmpty(path)) return;
        RootPathTextBox.Text = path;
    }
}
