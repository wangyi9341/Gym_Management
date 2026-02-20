using System.Globalization;
using System.Windows.Data;
using GymManager.Domain.Enums;

namespace GymManager.App.Converters;

public sealed class GenderToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Gender g
            ? g switch
            {
                Gender.Male => "男",
                Gender.Female => "女",
                _ => "未知"
            }
            : "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

