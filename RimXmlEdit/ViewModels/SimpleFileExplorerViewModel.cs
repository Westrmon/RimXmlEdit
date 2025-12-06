using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Models;
using RimXmlEdit.Service;
using RimXmlEdit.Views.DialogViews;

namespace RimXmlEdit.ViewModels;

public partial class SimpleFileExplorerViewModel : ObservableObject, ISimpleFileExplorer
{
    private readonly ILogger _log;
    private readonly string _rootPath;
    private string _currentPath;

    [ObservableProperty] private bool _isLoading;

    private bool _isSearched;

    [ObservableProperty] private bool _isSearchText;

    [ObservableProperty] private string _searchText;

    private FileSystemWatcher? _watcher;

    public SimpleFileExplorerViewModel()
    {
        _log = this.Log();
        _rootPath = TempConfig.ProjectPath;
        _currentPath = _rootPath;
        NavigateTo(_currentPath);
    }

    public ObservableCollection<FileSystemItemViewModel> Items { get; } = new();

    public event EventHandler<FileSystemItem>? OnOpenFile;

    public event EventHandler<TemplateXmlViewModel>? OnCreateTemplate;

    public event Action<string>? OnDeleteFile;

    [RelayCommand]
    private void Search(string? str)
    {
        if (string.IsNullOrWhiteSpace(str))
        {
            if (_isSearched)
                NavigateTo(_currentPath);
            return;
        }

        IEnumerable<FileSystemItem> items = null!;
        if (!IsSearchText)
        {
            var dirInfo = new DirectoryInfo(_currentPath);
            items = dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories)
                .Where(e => e.Name.StartsWith(str))
                .Select(fsi => new FileSystemItem(fsi.FullName));
        }
        else
        {
            items = FileStrSearch.Search(_currentPath, str, new FileStrSearch.SearchOptions
            {
                CaseSensitive = false,
                FileExtensions = ["*.xml"],
                UseParallelProcessing = false
            }).Where(e => string.IsNullOrEmpty(e.ErrorMessage)).Select(p => new FileSystemItem(p.FilePath));
        }

        Items.Clear();
        foreach (var item in items) Items.Add(new FileSystemItemViewModel(item));
        _isSearched = true;
    }

    private async Task NavigateTo(string? path)
    {
        if ((!_isSearched && string.IsNullOrWhiteSpace(path))
            || !Directory.Exists(path)
            || Path.GetRelativePath(_rootPath, path).StartsWith(".."))
            return;

        _currentPath = path;
        IsLoading = true;
        Items.Clear();
        SetupWatcher(path);

        try
        {
            var directoryItems = await Task.Run(() =>
            {
                var dirInfo = new DirectoryInfo(path);
                // Combine directories and files, with directories first
                var items = dirInfo.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly)
                    .Where(fsi => fsi is DirectoryInfo
                                  || (fsi is FileInfo fi
                                      && (fi.Extension.Equals(".xml", StringComparison.OrdinalIgnoreCase)
                                          || fi.Extension.Equals(".png", StringComparison.OrdinalIgnoreCase))))
                    .Select(fsi => new FileSystemItem(fsi.FullName))
                    .ToList();
                return items;
            });

            foreach (var item in directoryItems) Items.Add(new FileSystemItemViewModel(item));
        }
        catch (UnauthorizedAccessException ex)
        {
            _log.LogError(ex, "Access denied to directory: {Path}", path);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void GoUp()
    {
        if (_isSearched)
        {
            SearchText = string.Empty;
            NavigateTo(_currentPath);
            return;
        }

        var parent = Directory.GetParent(_currentPath);
        if (parent != null) NavigateTo(parent.FullName);
    }

    [RelayCommand]
    public void Open(FileSystemItemViewModel item)
    {
        if (item == null) return;

        if (item.IsDirectory)
            NavigateTo(item.FullName);
        else
            try
            {
                //Process.Start(new ProcessStartInfo(item.FullName) { UseShellExecute = true });
                OnOpenFile?.Invoke(this, item.Model);
            }
            catch (Exception)
            {
            }
    }

    [RelayCommand]
    public void Delete(FileSystemItemViewModel selectedItem)
    {
        if (selectedItem == null) return;
        Task.Run(() =>
        {
            try
            {
                if (selectedItem.IsDirectory)
                    Directory.Delete(selectedItem.FullName, true);
                else
                {
                    File.Delete(selectedItem.FullName);
                    OnDeleteFile?.Invoke(selectedItem.FullName);
                }
            }
            catch (Exception)
            {
                _log.LogError("Failed to delete file or directory: {Path}", selectedItem.FullName);
            }
        });
    }

    [RelayCommand]
    public void Copy(IEnumerable? selectedItems)
    {
        var items = selectedItems?.OfType<FileSystemItemViewModel>().ToList();
        if (items != null && items.Any()) ClipboardService.SetCopiedFiles(items.Select(i => i.FullName), false);
    }

    [RelayCommand]
    public void Cut(IEnumerable? selectedItems)
    {
        var items = selectedItems?.OfType<FileSystemItemViewModel>().ToList();
        if (items != null && items.Any()) ClipboardService.SetCopiedFiles(items.Select(i => i.FullName), true);
    }

    [RelayCommand]
    public async Task Create(string createType)
    {
        var isFile = createType == "file";
        Control diaglogView;
        if (!isFile)
            diaglogView = new GetTextView();
        else
            diaglogView = new TemplateXmlView();

        var result = await DialogHost.Show(diaglogView, "RootDialogHost");
        if (result is string returnedText && !string.IsNullOrEmpty(returnedText))
        {
            var newPath = Path.Combine(_currentPath, returnedText);
            if (isFile && diaglogView.DataContext is TemplateXmlViewModel vm)
            {
                if (!File.Exists(newPath))
                {
                    var hasSuffix = returnedText.AsSpan().IndexOf('.') > 0;
                    if (!hasSuffix)
                        newPath += ".xml";
                    await File.Create(newPath).DisposeAsync();
                    OnOpenFile?.Invoke(this, new FileSystemItem(newPath, false));
                    OnCreateTemplate?.Invoke(diaglogView, vm);
                }
            }
            else if (diaglogView.DataContext is GetTextViewModel)
            {
                if (!Directory.Exists(newPath))
                    Directory.CreateDirectory(newPath);
            }
        }
    }

    [RelayCommand]
    public async Task Paste()
    {
        if (!ClipboardService.CopiedFiles.Any()) return;

        await Task.Run(() =>
        {
            foreach (var sourcePath in ClipboardService.CopiedFiles)
            {
                var destPath = Path.Combine(_currentPath, Path.GetFileName(sourcePath));
                try
                {
                    if (Directory.Exists(sourcePath)) // It's a directory
                        Directory.CreateDirectory(destPath);
                    else
                        File.Copy(sourcePath, destPath, true);

                    if (ClipboardService.IsCut)
                    {
                        if (Directory.Exists(sourcePath)) Directory.Delete(sourcePath, true);
                        else File.Delete(sourcePath);
                    }
                }
                catch (Exception)
                {
                    // Handle exceptions
                }
            }

            if (ClipboardService.IsCut) ClipboardService.Clear();
        });
    }

    [RelayCommand]
    private void Rename()
    {
    }

    private void SetupWatcher(string path)
    {
        _watcher?.Dispose();
        _watcher = new FileSystemWatcher(path)
        {
            EnableRaisingEvents = true,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName
        };

        _watcher.Created += OnFileSystemChanged;
        _watcher.Deleted += OnFileSystemChanged;
        _watcher.Renamed += OnFileSystemRenamed;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        Dispatcher.UIThread.Post(() => NavigateTo(_currentPath));
    }

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
    {
        Dispatcher.UIThread.Post(() => NavigateTo(_currentPath));
    }
}