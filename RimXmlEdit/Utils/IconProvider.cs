using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RimXmlEdit.Utils;

public static class IconProvider
{
    private static readonly ConcurrentDictionary<string, WeakReference<Bitmap>> _iconCache = new();
    private static Bitmap? _defaultFileIcon;
    private static Bitmap? _defaultFolderIcon;

    // Load default icons once
    static IconProvider()
    {
        Task.Run(async () =>
        {
            _defaultFileIcon = await LoadIconFromAssets("avares://RimXmlEdit/Assets/images/file.png");
            _defaultFolderIcon = await LoadIconFromAssets("avares://RimXmlEdit/Assets/images/folder.png");
        });
    }

    public static async Task<Bitmap?> GetIconAsync(string path, bool isDirectory)
    {
        string key = isDirectory ? "::folder::" : Path.GetExtension(path).ToLower();

        if (string.IsNullOrEmpty(key))
        {
            key = "::file::"; // Default file key
        }

        if (_iconCache.TryGetValue(key, out var weakRef) && weakRef.TryGetTarget(out var cachedIcon))
        {
            return cachedIcon;
        }

        // In a real-world app, you would have platform-specific logic here to get system icons. For
        // simplicity, we use default icons.
        Bitmap? icon = isDirectory ? _defaultFolderIcon : _defaultFileIcon;

        if (icon != null)
        {
            _iconCache[key] = new WeakReference<Bitmap>(icon);
        }

        return icon;
    }

    private static Task<Bitmap> LoadIconFromAssets(string uri)
    {
        return Task.Run(() =>
        {
            var assets = AssetLoader.Open(new Uri(uri));
            return new Bitmap(assets);
        });
    }
}
