using System;
using Avalonia.Platform.Storage;

namespace RimXmlEdit.Utils;

internal class GlobalSingletonHelper
{
    public static IStorageProvider StorageProvider { get; set; }

    public static IServiceProvider Service { get; set; }

    public static ILauncher Launcher { get; set; }

    public static event Action? OnApplicationExiting;

    public static void Exit(object? sender, EventArgs e)
    {
        if (sender is not App) return;
        OnApplicationExiting?.Invoke();
    }
}