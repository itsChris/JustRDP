using System.Globalization;
using System.Windows.Data;
using JustRDP.Domain.Enums;
using MaterialDesignThemes.Wpf;

namespace JustRDP.Presentation.Converters;

public class TreeEntryTypeToIconConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var entryType = values.Length > 0 && values[0] is TreeEntryType t ? t : TreeEntryType.Connection;
        var connectionType = values.Length > 1 && values[1] is ConnectionType ct ? ct : (ConnectionType?)null;
        var isDashboard = values.Length > 2 && values[2] is true;

        if (isDashboard)
            return PackIconKind.Home;

        if (entryType == TreeEntryType.Folder)
            return PackIconKind.Folder;

        return connectionType switch
        {
            ConnectionType.SSH => PackIconKind.Console,
            _ => PackIconKind.MonitorDashboard
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
