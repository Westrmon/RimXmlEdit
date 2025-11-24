using Avalonia.Platform.Storage;
using System;

namespace RimXmlEdit.Utils;

internal class GlobalSingletonHelper
{
    public static IStorageProvider StorageProvider { get; set; }

    public static IServiceProvider Service { get; set; }
}
