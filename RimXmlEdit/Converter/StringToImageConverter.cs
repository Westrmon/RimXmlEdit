using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Globalization;

namespace RimXmlEdit.Converter;

public class StringToImageConverter : IValueConverter
{
    /// <summary>
    /// A static instance of the converter for use in XAML.
    /// </summary>
    public static readonly StringToImageConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string rawUri || string.IsNullOrEmpty(rawUri))
        {
            return null;
        }

        try
        {
            var uri = new Uri(rawUri, UriKind.Absolute);
            using (var stream = AssetLoader.Open(uri))
            {
                return new Bitmap(stream);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[StringToImageConverter Error] Could not load image from URI '{rawUri}'. Exception: {ex.Message}");
            return null;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
