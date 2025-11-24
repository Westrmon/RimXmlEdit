using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace RimXmlEdit.Converter;

public class BoolToIntegerConverter : IValueConverter
{
    /// <summary>
    /// A static instance of the converter to be used in XAML.
    /// </summary>
    public static readonly BoolToIntegerConverter Instance = new();

    /// <summary>
    /// Converts a boolean to an integer (0 for true, 1 for false).
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? 0 : 1;
        }
        return 0; // Default to the first tab
    }

    /// <summary>
    /// Converts an integer back to a boolean (true for 0, false otherwise).
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i)
        {
            return i == 0;
        }
        return true; // Default to true if conversion fails
    }
}
