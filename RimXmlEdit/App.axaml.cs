using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Extensions;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Utils;
using RimXmlEdit.ViewModels;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace RimXmlEdit;

public partial class App : Application
{
    private ILogger _log;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppDomain.CurrentDomain.UnhandledException += HandleGlobalException;
        Dispatcher.UIThread.UnhandledException += UIThread_UnhandledException;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        // Line below is needed to remove Avalonia data validation. Without this line you will get
        // duplicate validations from both Avalonia and CT
        BindingPlugins.DataValidators.RemoveAt(0);

        var collection = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        collection.AddCommonServices(configuration);
        var services = collection.BuildServiceProvider();
        _log = this.Log();
        GlobalSingletonHelper.Service = services;
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var localizationService = services.GetRequiredService<ILocalizationService>();
                var vm = services.GetRequiredService<InitViewModel>();

                desktop.MainWindow = new InitWindow
                {
                    DataContext = vm
                };
                desktop.Exit += (_, _) =>
                {
                };

                localizationService.SwitchLanguage(new CultureInfo("zh-CN"));
                var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
                GlobalSingletonHelper.StorageProvider = topLevel.StorageProvider;
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to initialize application");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Dispatcher.UIThread.InvokeAsync(() => ShowCrashWindow(e.Exception));
        e.SetObserved();
    }

    private void UIThread_UnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ShowCrashWindow(e.Exception);
    }

    private void CurrentDomain_ProcessExit(object? sender, EventArgs e)
    {
        _log.LogInformation("Application is exiting");
    }

    private void HandleGlobalException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Dispatcher.UIThread.InvokeAsync(() => ShowCrashWindow(ex));
        }
    }

    private void ShowCrashWindow(Exception exception)
    {
        _log.LogError(exception, "Error");
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            foreach (var win in desktop.Windows)
            {
                if (win is CrashReportWindow)
                    return;
            }
        }

        var crashWindow = new CrashReportWindow();
        crashWindow.ErrorMessageBox.Text = exception.ToString();
        if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop2
            && desktop2.MainWindow != null)
        {
            crashWindow.ShowDialog(desktop2.MainWindow);
        }
        else
        {
            crashWindow.Show();
        }
    }
}
