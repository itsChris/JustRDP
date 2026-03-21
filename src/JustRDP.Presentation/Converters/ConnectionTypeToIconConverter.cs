using System.Globalization;
using System.Windows.Data;
using JustRDP.Domain.Enums;
using MaterialDesignThemes.Wpf;

namespace JustRDP.Presentation.Converters;

public class ConnectionTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ConnectionType ct && ct == ConnectionType.SSH
            ? PackIconKind.Console
            : PackIconKind.MonitorDashboard;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
