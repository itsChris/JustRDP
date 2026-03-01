using System.Globalization;
using System.Windows.Data;
using JustRDP.Domain.Enums;
using MaterialDesignThemes.Wpf;

namespace JustRDP.Presentation.Converters;

public class TreeEntryTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is TreeEntryType type
            ? type == TreeEntryType.Folder ? PackIconKind.Folder : PackIconKind.MonitorDashboard
            : PackIconKind.File;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
