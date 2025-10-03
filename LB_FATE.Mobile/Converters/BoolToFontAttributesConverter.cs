using System.Globalization;

namespace LB_FATE.Mobile.Converters;

/// <summary>
/// 将布尔值转换为FontAttributes (True = Bold, False = None)
/// </summary>
public class BoolToFontAttributesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isBold && isBold)
        {
            return FontAttributes.Bold;
        }
        return FontAttributes.None;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
