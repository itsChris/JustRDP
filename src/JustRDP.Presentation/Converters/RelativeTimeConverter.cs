using System.Globalization;
using System.Windows.Data;

namespace JustRDP.Presentation.Converters;

public class RelativeTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dt)
            return "Never";

        var elapsed = DateTime.UtcNow - dt;

        if (elapsed.TotalMinutes < 1)
            return "Just now";
        if (elapsed.TotalMinutes < 60)
            return $"{(int)elapsed.TotalMinutes} min ago";
        if (elapsed.TotalHours < 24)
            return $"{(int)elapsed.TotalHours} hours ago";
        if (elapsed.TotalDays < 7)
            return $"{(int)elapsed.TotalDays} days ago";

        return dt.ToLocalTime().ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
