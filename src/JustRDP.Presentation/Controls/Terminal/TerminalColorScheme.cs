using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace JustRDP.Presentation.Controls.Terminal;

public sealed class TerminalColorScheme
{
    private readonly Color[] _palette = new Color[256];

    public Color DefaultForeground { get; }
    public Color DefaultBackground { get; }

    public TerminalColorScheme(Color? defaultForeground = null, Color? defaultBackground = null)
    {
        DefaultForeground = defaultForeground ?? Color.FromRgb(0xCC, 0xCC, 0xCC);
        DefaultBackground = defaultBackground ?? Color.FromRgb(0x1E, 0x1E, 0x1E);

        // ANSI 16 colors — VS Code Dark theme
        _palette[0] = Color.FromRgb(0x1E, 0x1E, 0x1E);  // Black
        _palette[1] = Color.FromRgb(0xCD, 0x31, 0x31);  // Red
        _palette[2] = Color.FromRgb(0x0D, 0xBC, 0x79);  // Green
        _palette[3] = Color.FromRgb(0xE5, 0xE5, 0x10);  // Yellow
        _palette[4] = Color.FromRgb(0x24, 0x72, 0xC0);  // Blue
        _palette[5] = Color.FromRgb(0xBC, 0x3F, 0xBC);  // Magenta
        _palette[6] = Color.FromRgb(0x11, 0xA8, 0xCD);  // Cyan
        _palette[7] = Color.FromRgb(0xE5, 0xE5, 0xE5);  // White
        _palette[8] = Color.FromRgb(0x66, 0x66, 0x66);  // Bright Black
        _palette[9] = Color.FromRgb(0xF1, 0x4C, 0x4C);  // Bright Red
        _palette[10] = Color.FromRgb(0x23, 0xD1, 0x8B); // Bright Green
        _palette[11] = Color.FromRgb(0xF5, 0xF5, 0x43); // Bright Yellow
        _palette[12] = Color.FromRgb(0x3B, 0x8E, 0xEA); // Bright Blue
        _palette[13] = Color.FromRgb(0xD6, 0x70, 0xD6); // Bright Magenta
        _palette[14] = Color.FromRgb(0x29, 0xB8, 0xDB); // Bright Cyan
        _palette[15] = Color.FromRgb(0xF5, 0xF5, 0xF5); // Bright White

        // 6x6x6 color cube (indices 16-231)
        for (int i = 16; i < 232; i++)
        {
            int index = i - 16;
            int ri = index / 36;
            int gi = (index / 6) % 6;
            int bi = index % 6;
            byte r = (byte)(ri == 0 ? 0 : 55 + ri * 40);
            byte g = (byte)(gi == 0 ? 0 : 55 + gi * 40);
            byte b = (byte)(bi == 0 ? 0 : 55 + bi * 40);
            _palette[i] = Color.FromRgb(r, g, b);
        }

        // Grayscale ramp (indices 232-255)
        for (int i = 232; i < 256; i++)
        {
            byte level = (byte)(8 + (i - 232) * 10);
            _palette[i] = Color.FromRgb(level, level, level);
        }
    }

    public Color GetColor(int index)
    {
        if (index is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(index), index, "Color index must be between 0 and 255.");
        return _palette[index];
    }

    public static Color FromRgb(byte r, byte g, byte b) => Color.FromRgb(r, g, b);

    public static Color FromHex(string hex)
    {
        if (string.IsNullOrEmpty(hex))
            return Colors.Transparent;

        ReadOnlySpan<char> span = hex.AsSpan();
        if (span[0] == '#')
            span = span[1..];

        if (span.Length == 6 &&
            byte.TryParse(span[..2], System.Globalization.NumberStyles.HexNumber, null, out byte r) &&
            byte.TryParse(span[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g) &&
            byte.TryParse(span[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b))
        {
            return Color.FromRgb(r, g, b);
        }

        return Colors.Transparent;
    }
}
