using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using JustRDP.Domain.Enums;

namespace JustRDP.Presentation.Converters;

public class AvailabilityStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush AvailableBrush = CreateFrozenBrush(0xFF, 0x4C, 0xAF, 0x50);
    private static readonly SolidColorBrush UnavailableBrush = CreateFrozenBrush(0xFF, 0xF4, 0x43, 0x36);
    private static readonly SolidColorBrush UnknownBrush = CreateFrozenBrush(0xFF, 0x9E, 0x9E, 0x9E);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is AvailabilityStatus status
            ? status switch
            {
                AvailabilityStatus.Available => AvailableBrush,
                AvailabilityStatus.Unavailable => UnavailableBrush,
                _ => UnknownBrush
            }
            : UnknownBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static SolidColorBrush CreateFrozenBrush(byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}
