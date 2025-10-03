using System.Globalization;

namespace LB_FATE.Mobile.Converters;

/// <summary>
/// 字符串到布尔值转换器 - 用于判断字符串是否非空
/// </summary>
public class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrWhiteSpace(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
