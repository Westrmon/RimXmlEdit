using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DialogHostAvalonia;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Models;
using RimXmlEdit.Utils;
using RimXmlEdit.Views;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RimXmlEdit.ViewModels;

public partial class SimpleFileExplorerViewModel : ObservableObject, ISimpleFileExplorer
{
    private string _rootPath;

    private string _currentPath;
    private bool _isSearched = false;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText;

    [ObservableProperty]
    private bool _isSearchText;

    public ObservableCollection<FileSystemItemViewModel> Items { get; } = new();

    private FileSystemWatcher? _watcher;

    public event EventHandler<FileSystemItem> OnOpenFile;

    private ILogger _log;

    public SimpleFileExplorerViewModel()
    {
        _log = this.Log();
        _rootPath = TempConfig.ProjectPath;
        _currentPath = _rootPath;
        //MainWindow.OnDialogClosing += MainWindow_OnDialogClosing;
        NavigateTo(_currentPath);
    }

    private void MainWindow_OnDialogClosing(object? sender, DialogClosingEventArgs e)
    {
        if (sender is GetTextView && e.Parameter is string text)
        {
            Debug.WriteLine(text);
        }
    }

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
        foreach (var item in items)
        {
            Items.Add(new FileSystemItemViewModel(item));
        }
        _isSearched = true;
    }

    private async Task NavigateTo(string? path)
    {
        if (!_isSearched && string.IsNullOrWhiteSpace(path)
            || !Directory.Exists(path)
            || Path.GetRelativePath(_rootPath, path).StartsWith(".."))
        {
            return;
        }

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

            foreach (var item in directoryItems)
            {
                Items.Add(new FileSystemItemViewModel(item));
            }
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
        if (parent != null)
        {
            NavigateTo(parent.FullName);
        }
    }

    [RelayCommand]
    public void Open(FileSystemItemViewModel item)
    {
        if (item == null) return;

        if (item.IsDirectory)
        {
            NavigateTo(item.FullName);
        }
        else
        {
            try
            {
                //Process.Start(new ProcessStartInfo(item.FullName) { UseShellExecute = true });
                OnOpenFile?.Invoke(this, item.Model);
            }
            catch (Exception)
            {
            }
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
                {
                    Directory.Delete(selectedItem.FullName, true);
                }
                else
                {
                    File.Delete(selectedItem.FullName);
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
        if (items != null && items.Any())
        {
            ClipboardService.SetCopiedFiles(items.Select(i => i.FullName), isCut: false);
        }
    }

    [RelayCommand]
    public void Cut(IEnumerable? selectedItems)
    {
        var items = selectedItems?.OfType<FileSystemItemViewModel>().ToList();
        if (items != null && items.Any())
        {
            ClipboardService.SetCopiedFiles(items.Select(i => i.FullName), isCut: true);
        }
    }

    [RelayCommand]
    public async Task Create()
    {
        var diaglogView = new GetTextView();
        var result = await DialogHost.Show(diaglogView, "RootDialogHost");
        if (result is string returnedText && !string.IsNullOrEmpty(returnedText))
        {
            var data = diaglogView.DataContext as GetTextViewModel;
            var newDir = Path.Combine(_currentPath, returnedText);
            if (data.IsFolder && !Directory.Exists(newDir))
                Directory.CreateDirectory(newDir);
            else if (!data.IsFolder && !File.Exists(newDir))
            {
                var hasSuffix = returnedText.AsSpan().IndexOf('.') > 0;
                if (!hasSuffix)
                    newDir += ".xml";
                File.Create(newDir).Dispose();
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
                string destPath = Path.Combine(_currentPath, Path.GetFileName(sourcePath));
                try
                {
                    if (Directory.Exists(sourcePath)) // It's a directory
                    {
                        Directory.CreateDirectory(destPath);
                    }
                    else
                    {
                        File.Copy(sourcePath, destPath, true);
                    }

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

            if (ClipboardService.IsCut)
            {
                ClipboardService.Clear();
            }
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
