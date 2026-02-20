using System.Globalization;
using System.Windows.Data;
using GymManager.App.ViewModels;

namespace GymManager.App.Converters;

public sealed class AnnualCardFilterToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is AnnualCardFilter f
            ? f switch
            {
                AnnualCardFilter.All => "全部",
                AnnualCardFilter.Normal => "正常",
                AnnualCardFilter.ExpiringSoon => "即将到期",
                AnnualCardFilter.Expired => "已过期",
                _ => "全部"
            }
            : "全部";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

