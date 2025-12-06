using System;
using System.Collections;
using System.Globalization;
using Avalonia.Data.Converters;

namespace RimXmlEdit.Converter;

public class HasValueConverter : IValueConverter
{
    public static readonly HasValueConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasItems = false;
        if (value is IEnumerable enumerable)
        {
            if (value is ICollection collection)
            {
                hasItems = collection.Count > 0;
            }
            else
            {
                var enumerator = enumerable.GetEnumerator();
                hasItems = enumerator.MoveNext();

                if (enumerator is IDisposable disposable) disposable.Dispose();
            }
        }

        if (parameter is string paramStr &&
            ((bool.TryParse(paramStr, out var invert) && invert) ||
             paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase)))
            return !hasItems;

        return hasItems;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}