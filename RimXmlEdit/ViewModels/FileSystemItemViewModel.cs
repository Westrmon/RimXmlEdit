using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using RimXmlEdit.Models;
using RimXmlEdit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.ViewModels;

public partial class FileSystemItemViewModel : ObservableObject
{
    public FileSystemItem Model { get; }

    public string Name => Model.Name;
    public string FullName => Model.FullName;
    public bool IsDirectory => Model.IsDirectory;
    public long Size => Model.Size;
    public DateTime LastModified => Model.LastModified;

    [ObservableProperty]
    private Bitmap? _icon;

    [ObservableProperty]
    private bool _isRenaming;

    public FileSystemItemViewModel(FileSystemItem model)
    {
        Model = model;
        LoadIcon();
    }

    private async void LoadIcon()
    {
        Icon = await IconProvider.GetIconAsync(FullName, IsDirectory);
    }
}
