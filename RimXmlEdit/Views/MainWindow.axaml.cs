using System;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using DialogHostAvalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RimXmlEdit.Core.Utils;
using RimXmlEdit.Utils;
using RimXmlEdit.ViewModels;
using TextMateSharp.Grammars;

namespace RimXmlEdit.Views;

public partial class MainWindow : Window
{
    private readonly TextEditor _textEditor;
    private WindowNotificationManager? _notificationManager;

    public MainWindow()
    {
        InitializeComponent();
        _textEditor = this.FindControl<TextEditor>("TextEdit") ??
                      throw new ArgumentNullException(nameof(TextEditor));
        var search = this.FindControl<QuickSearchBox>("SearchBox") ??
                     throw new ArgumentNullException(nameof(TextEditor));
        _textEditor.IsReadOnly = false;
        _textEditor.Options.HighlightCurrentLine = true;

        _textEditor.Options.EnableHyperlinks = true;
        _textEditor.Options.EnableTextDragDrop = true;
        _textEditor.Options.WordWrapIndentation = 4;

        var theme = ActualThemeVariant == ThemeVariant.Light
            ? ThemeName.LightPlus
            : ThemeName.DarkPlus;
        var options = new RegistryOptions(theme);
        var installation = _textEditor.InstallTextMate(options);

        installation.SetGrammar(options.GetScopeByLanguageId(options.GetLanguageByExtension(".xml").Id));
        search.DataContext = GlobalSingletonHelper.Service.GetRequiredService<QuickSearchBoxViewModel>();
    }

    public static event EventHandler<MultTappedEventArgs>? OnDoubleTapped;

    public static event EventHandler<DialogClosingEventArgs>? OnDialogClosing;

    public static event EventHandler<TextChangedEventArgs>? OnInputNodeValue;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _notificationManager = new WindowNotificationManager(this)
        {
            Position = NotificationPosition.BottomRight,
            MaxItems = 3
        };

        LoggerFactoryInstance.OnShowNotification = (level, category, message) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                var type = level switch
                {
                    LogLevel.Information => NotificationType.Information,
                    LogLevel.Warning => NotificationType.Warning,
                    LogLevel.Error or LogLevel.Critical => NotificationType.Error,
                    _ => NotificationType.Information
                };

                _notificationManager.Show(new Notification(
                    level.ToString(),
                    message,
                    type,
                    TimeSpan.FromSeconds(5)
                ));
            });
        };
    }

    private void TextBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        OnDoubleTapped?.Invoke(sender, new MultTappedEventArgs(nameof(TextBox)));
    }

    private void DialogHost_DialogClosing(object? sender, DialogClosingEventArgs e)
    {
        OnDialogClosing?.Invoke(sender, e);
    }

    private void ListBox_DoubleTapped(object? sender, TappedEventArgs e)
    {
        OnDoubleTapped?.Invoke(sender, new MultTappedEventArgs(nameof(ListBox)));
    }

    private void TextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        OnInputNodeValue?.Invoke(sender, e);
    }
}

public class MultTappedEventArgs : EventArgs
{
    public MultTappedEventArgs(string sourceTypeName)
    {
        SourceTypeName = sourceTypeName;
    }

    public bool Handle { get; set; } = false;
    public string SourceTypeName { get; }
}