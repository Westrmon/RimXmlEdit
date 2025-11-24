// src/ModDefEditor/Converters/NullOrEmptyStringToBoolConverter.cs
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace RimXmlEdit.Converter;

/// <summary>
/// Converts a string to a boolean value. Returns true if the string is null or empty; otherwise, false.
/// </summary>
public class NullOrEmptyStringToBoolConverter : IValueConverter
{
    public static readonly NullOrEmptyStringToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is not string str || string.IsNullOrEmpty(str);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
