using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace RimXmlEdit.Converter;

/// <summary>
/// Inverts a boolean value. True becomes False, and False becomes True.
/// </summary>
public class BoolNotConverter : IValueConverter
{
    /// <summary>
    /// A static instance of the converter to be used in XAML.
    /// </summary>
    public static readonly BoolNotConverter Instance = new();

    /// <summary>
    /// Inverts the boolean value.
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return false;
    }

    /// <summary>
    /// Inverts the boolean value back (which is the same operation).
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return true;
    }
}
