using System.Globalization;
using System.Windows.Data;
using GymManager.Domain.Entities;

namespace GymManager.App.Converters;

/// <summary>
/// 将“即将到期会员列表”转换成短文本：张三(02-20)、李四(02-21)…
/// </summary>
public sealed class AnnualCardReminderTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not IEnumerable<AnnualCardMember> list)
        {
            return string.Empty;
        }

        var items = list
            .OrderBy(x => x.EndDate)
            .Take(3)
            .Select(x => $"{x.Name}({x.EndDate:MM-dd})")
            .ToList();

        if (items.Count == 0)
        {
            return string.Empty;
        }

        var hasMore = list is ICollection<AnnualCardMember> c ? c.Count > 3 : list.Skip(3).Any();
        return string.Join("、", items) + (hasMore ? "…" : string.Empty);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

