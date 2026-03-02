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
    private ILogger<SshTerminalControl>? _logger;

    public bool IsSessionConnected => _session?.IsConnected == true;

    public event Action? SessionConnected;
    public event Action? SessionDisconnected;
    public event Action<Exception>? SessionError;
    public event Action? SessionInteractivePrompt;

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
        _logger = loggerFactory.CreateLogger<SshTerminalControl>();
        _logger.LogDebug("[SshTermCtrl] LoggerFactory set");
    }

    public async Task ConnectAsync(TerminalOptions options)
    {
        _logger?.LogDebug("[SshTermCtrl] ConnectAsync called for {Host}:{Port}", options.Host, options.Port);

        if (_session != null)
        {
            _logger?.LogDebug("[SshTermCtrl] Existing session found, disconnecting first");
            Disconnect();
        }

        _colorScheme = new TerminalColorScheme(
            TerminalColorScheme.FromHex(options.ForegroundColor),
            TerminalColorScheme.FromHex(options.BackgroundColor));

        _logger?.LogDebug("[SshTermCtrl] Creating TerminalSession");
        _session = new TerminalSession(Dispatcher, _loggerFactory.CreateLogger<TerminalSession>());
        _session.Connected += OnSessionConnected;
        _session.Disconnected += OnSessionDisconnected;
        _session.ErrorOccurred += OnSessionError;
        _session.InteractivePromptStarted += OnInteractivePromptStarted;
        _logger?.LogDebug("[SshTermCtrl] Session events wired (Connected, Disconnected, ErrorOccurred, InteractivePromptStarted)");

        TerminalArea.SetFont(options.FontFamily, options.FontSize);
        TerminalArea.CalculateDimensions();

        int cols = Math.Max(1, TerminalArea.VisibleColumns);
        int rows = Math.Max(1, TerminalArea.VisibleRows);
        _logger?.LogDebug("[SshTermCtrl] Terminal dimensions: {Cols}x{Rows}, font={Font}@{Size}",
            cols, rows, options.FontFamily, options.FontSize);

        TerminalArea.Attach(_session, _colorScheme);
        _logger?.LogDebug("[SshTermCtrl] TerminalArea attached");

        _inputHandler = new TerminalInputHandler(_session, TerminalArea);
        _logger?.LogDebug("[SshTermCtrl] InputHandler created, calling session.ConnectAsync");

        await _session.ConnectAsync(options, cols, rows);
        _logger?.LogDebug("[SshTermCtrl] session.ConnectAsync returned");
    }

    public void Disconnect()
    {
        _logger?.LogDebug("[SshTermCtrl] Disconnect called");

        TerminalArea.Detach();
        _inputHandler = null;

        if (_session != null)
        {
            _logger?.LogDebug("[SshTermCtrl] Disposing session");
            _session.Connected -= OnSessionConnected;
            _session.Disconnected -= OnSessionDisconnected;
            _session.ErrorOccurred -= OnSessionError;
            _session.InteractivePromptStarted -= OnInteractivePromptStarted;
            _session.Dispose();
            _session = null;
        }

        _colorScheme = null;
        _logger?.LogDebug("[SshTermCtrl] Disconnect complete");
    }

    private void OnSessionConnected()
    {
        _logger?.LogDebug("[SshTermCtrl] OnSessionConnected event received, firing SessionConnected");
        SessionConnected?.Invoke();
    }

    private void OnSessionDisconnected()
    {
        _logger?.LogDebug("[SshTermCtrl] OnSessionDisconnected event received, firing SessionDisconnected");
        SessionDisconnected?.Invoke();
    }

    private void OnSessionError(Exception ex)
    {
        _logger?.LogError(ex, "[SshTermCtrl] OnSessionError event received, firing SessionError");
        SessionError?.Invoke(ex);
    }

    private void OnInteractivePromptStarted()
    {
        _logger?.LogDebug("[SshTermCtrl] OnInteractivePromptStarted, firing SessionInteractivePrompt");
        SessionInteractivePrompt?.Invoke();
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
