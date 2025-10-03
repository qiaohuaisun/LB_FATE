using System.Globalization;

namespace LB_FATE.Mobile.Converters;

/// <summary>
/// 布尔值到颜色转换器 - 支持通过参数指定true/false颜色
/// 参数格式: "TrueColor:FalseColor" (如 "#1A7F37:#656D76")
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // 如果提供了参数，解析颜色
            if (parameter is string colorPair && colorPair.Contains(':'))
            {
                var colors = colorPair.Split(':');
                if (colors.Length == 2)
                {
                    var trueColor = Color.FromArgb(colors[0].Trim());
                    var falseColor = Color.FromArgb(colors[1].Trim());
                    return boolValue ? trueColor : falseColor;
                }
            }

            // 默认颜色
            return boolValue ? Colors.Green : Colors.Orange;
        }

        return Colors.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
