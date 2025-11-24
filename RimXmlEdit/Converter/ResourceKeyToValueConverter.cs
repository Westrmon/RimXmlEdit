using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace RimXmlEdit.Converter;

public class ResourceKeyToValueConverter : IValueConverter
{
    public static readonly ResourceKeyToValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || string.IsNullOrEmpty(key))
        {
            return AvaloniaProperty.UnsetValue;
        }

        if (Application.Current != null && Application.Current.TryFindResource(key, out var resourceValue))
        {
            return resourceValue;
        }

        return $"#{key}#";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
