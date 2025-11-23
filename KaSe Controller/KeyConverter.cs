using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace KaSe_Controller;

public class KeyConverter : IValueConverter
{
    public static readonly KeyConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, 
        CultureInfo culture)
    {
        if (value is K_Keys key)
        {
            return key.ToString().Replace("K_", "").Replace("MO_L", "MO L").Replace("TO_L", "TO L")
                .Replace("MACRO_", "M").Replace("HID_KEY_","");
        }
        return BindingOperations.DoNothing;
    }

    public object ConvertBack(object? value, Type targetType, 
        object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
