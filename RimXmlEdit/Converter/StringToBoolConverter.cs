using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace RimXmlEdit.Converter;

public class StringToBoolConverter : IValueConverter
{
    /// <summary>
    /// A static instance of the converter to be used in XAML.
    /// </summary>
    public static readonly StringToBoolConverter Instance = new();

    /// <summary>
    /// Converts a string to a boolean.
    /// </summary>
    /// <param name="value"> The string to convert. </param>
    /// <param name="targetType"> The type of the binding target property. </param>
    /// <param name="parameter"> The converter parameter to use. </param>
    /// <param name="culture"> The culture to use in the converter. </param>
    /// <returns> True if the string is not null or empty; otherwise, false. </returns>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str)
        {
            return !string.IsNullOrEmpty(str);
        }
        return false;
    }

    /// <summary>
    /// This converter does not support converting back.
    /// </summary>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
