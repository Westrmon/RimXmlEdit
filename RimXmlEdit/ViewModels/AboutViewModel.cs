using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RimXmlEdit.Core.Utils;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace RimXmlEdit.ViewModels;

public partial class AboutViewModel : ViewModelBase
{
    public AboutViewModel()
    {
        var assembly = Assembly.GetEntryAssembly();
        var v = TempConfig.AppVersion;
        AppVersion = $"{v.Major}.{v.Minor}.{v.Build} (Build {v.Revision})";

        Contributors = new ObservableCollection<string>();
        Contributors.CollectionChanged += (s, e) => IsContributorsVisible = Contributors.Count > 0;
    }

    [ObservableProperty]
    private string _appVersion;

    [ObservableProperty]
    private string _author = "Westrmon";

    // 链接配置
    public string GithubUrl => "https://github.com/Westrmon/RimXmlEdit";

    public string QQGroupUrl => "https://qm.qq.com/q/rPAdFobOVy";

    // 贡献者列表
    public ObservableCollection<string> Contributors { get; }

    [ObservableProperty]
    private bool _isContributorsVisible = false;

    [RelayCommand]
    private void OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to open URL: {ex.Message}");
        }
    }
}
