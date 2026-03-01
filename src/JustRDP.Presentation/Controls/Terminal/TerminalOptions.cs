namespace JustRDP.Presentation.Controls.Terminal;

public sealed class TerminalOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 22;
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? PrivateKeyPath { get; init; }
    public string? PrivateKeyPassphrase { get; init; }
    public string FontFamily { get; init; } = "Consolas";
    public double FontSize { get; init; } = 14;
    public string BackgroundColor { get; init; } = "#1E1E1E";
    public string ForegroundColor { get; init; } = "#CCCCCC";
    public string TerminalType { get; init; } = "xterm-256color";
    public int BufferSize { get; init; } = 65536;
}
