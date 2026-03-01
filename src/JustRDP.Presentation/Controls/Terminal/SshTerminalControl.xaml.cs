using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace JustRDP.Presentation.Controls.Terminal;

public partial class SshTerminalControl : System.Windows.Controls.UserControl
{
    private TerminalSession? _session;
    private TerminalInputHandler? _inputHandler;
    private TerminalColorScheme? _colorScheme;
    private readonly DispatcherTimer _resizeTimer;
    private ILoggerFactory _loggerFactory = NullLoggerFactory.Instance;

    public bool IsSessionConnected => _session?.IsConnected == true;

    public event Action? SessionConnected;
    public event Action? SessionDisconnected;
    public event Action<Exception>? SessionError;

    public SshTerminalControl()
    {
        InitializeComponent();

        _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _resizeTimer.Tick += OnResizeTimerTick;

        TerminalArea.SizeChanged += OnTerminalAreaSizeChanged;
    }

    public void SetLoggerFactory(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public async Task ConnectAsync(TerminalOptions options)
    {
        if (_session != null)
            Disconnect();

        _colorScheme = new TerminalColorScheme(
            TerminalColorScheme.FromHex(options.ForegroundColor),
            TerminalColorScheme.FromHex(options.BackgroundColor));

        _session = new TerminalSession(Dispatcher, _loggerFactory.CreateLogger<TerminalSession>());
        _session.Connected += OnSessionConnected;
        _session.Disconnected += OnSessionDisconnected;
        _session.ErrorOccurred += OnSessionError;

        TerminalArea.SetFont(options.FontFamily, options.FontSize);
        TerminalArea.CalculateDimensions();

        int cols = Math.Max(1, TerminalArea.VisibleColumns);
        int rows = Math.Max(1, TerminalArea.VisibleRows);

        TerminalArea.Attach(_session, _colorScheme);

        _inputHandler = new TerminalInputHandler(_session, TerminalArea);

        await _session.ConnectAsync(options, cols, rows);
    }

    public void Disconnect()
    {
        TerminalArea.Detach();
        _inputHandler = null;

        if (_session != null)
        {
            _session.Connected -= OnSessionConnected;
            _session.Disconnected -= OnSessionDisconnected;
            _session.ErrorOccurred -= OnSessionError;
            _session.Dispose();
            _session = null;
        }

        _colorScheme = null;
    }

    private void OnSessionConnected()
    {
        SessionConnected?.Invoke();
    }

    private void OnSessionDisconnected()
    {
        SessionDisconnected?.Invoke();
    }

    private void OnSessionError(Exception ex)
    {
        SessionError?.Invoke(ex);
    }

    // Keyboard routing
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        _inputHandler?.HandleKeyDown(e);
        if (!e.Handled)
            base.OnPreviewKeyDown(e);
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        _inputHandler?.HandleTextInput(e);
        if (!e.Handled)
            base.OnPreviewTextInput(e);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        Focus();
        base.OnMouseLeftButtonDown(e);
    }

    // Resize handling with debounce
    private void OnTerminalAreaSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _resizeTimer.Stop();
        _resizeTimer.Start();
    }

    private void OnResizeTimerTick(object? sender, EventArgs e)
    {
        _resizeTimer.Stop();

        if (_session == null || !_session.IsConnected)
            return;

        TerminalArea.CalculateDimensions();
        int cols = Math.Max(1, TerminalArea.VisibleColumns);
        int rows = Math.Max(1, TerminalArea.VisibleRows);

        if (cols == _session.Columns && rows == _session.Rows)
            return;

        _session.ResizeTerminal(cols, rows);
        _session.SendWindowChangeRequest(cols, rows);
    }
}
