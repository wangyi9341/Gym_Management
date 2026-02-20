using System.Globalization;
using System.Windows.Data;
using GymManager.Domain.Enums;

namespace GymManager.App.Converters;

public sealed class AnnualCardStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is AnnualCardStatus s
            ? s switch
            {
                AnnualCardStatus.Normal => "正常",
                AnnualCardStatus.ExpiringSoon => "即将到期",
                AnnualCardStatus.Expired => "已过期",
                _ => "未知"
            }
            : "未知";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

