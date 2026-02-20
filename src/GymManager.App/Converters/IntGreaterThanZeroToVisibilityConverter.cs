using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GymManager.App.Converters;

/// <summary>
/// int &gt; 0 => Visible，否则 Collapsed。
/// </summary>
public sealed class IntGreaterThanZeroToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}

