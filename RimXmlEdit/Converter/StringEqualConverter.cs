using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace RimXmlEdit.Converter;

public class StringEqualConverter : IValueConverter
{
    public static readonly StringEqualConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Equals(value, parameter);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return (bool)value ? parameter : null;
    }
}