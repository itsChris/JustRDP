using System.Text;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;
using VtNetCore.VirtualTerminal;
using VtNetCore.XTermParser;

namespace JustRDP.Presentation.Controls.Terminal;

public sealed class TerminalSession : IDisposable
{
    private SshClient? _client;
    private ShellStream? _stream;
    private Thread? _readThread;
    private CancellationTokenSource? _cts;

    private readonly VirtualTerminalController _terminal;
    private readonly DataConsumer _dataConsumer;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<TerminalSession> _logger;

    // Input capture for interactive auth
    private volatile bool _isCapturingInput;
    private bool _captureEcho;
    private bool _inputCancelled;
    private readonly StringBuilder _inputBuffer = new();
    private TaskCompletionSource<string>? _inputTcs;
    private readonly ManualResetEventSlim _inputReadyEvent = new(false);
    private string? _blockingInputResult;

    public VirtualTerminalController Terminal => _terminal;
    public bool IsConnected => _client?.IsConnected == true;
    public bool IsCapturingInput => _isCapturingInput;
    public int Columns { get; private set; } = 80;
    public int Rows { get; private set; } = 24;
    public int CursorRow => _terminal.CursorState.CurrentRow;
    public int CursorColumn => _terminal.CursorState.CurrentColumn;

    public event Action? TerminalUpdated;
    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<Exception>? ErrorOccurred;
    public event Action? InteractivePromptStarted;

    public TerminalSession(Dispatcher dispatcher, ILogger<TerminalSession> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _terminal = new VirtualTerminalController();
        _terminal.ResizeView(80, 24);
        _dataConsumer = new DataConsumer(_terminal);
        _logger.LogDebug("[Session] TerminalSession created");
    }

    public async Task ConnectAsync(TerminalOptions options, int columns, int rows, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[Session] ConnectAsync called: host={Host}, port={Port}, cols={Cols}, rows={Rows}",
            options.Host, options.Port, columns, rows);

        if (string.IsNullOrWhiteSpace(options.Host))
            throw new ArgumentException("Host is required.", nameof(options));
        if (options.Port is < 1 or > 65535)
            throw new ArgumentException("Port must be between 1 and 65535.", nameof(options));

        Columns = columns;
        Rows = rows;
        _terminal.ResizeView(columns, rows);

        var username = options.Username ?? string.Empty;
        bool hasPrivateKey = !string.IsNullOrEmpty(options.PrivateKeyPath);
        bool hasPassword = !string.IsNullOrEmpty(options.Password);
        bool hasUsername = !string.IsNullOrWhiteSpace(username);

        _logger.LogDebug("[Session] Credential state: hasUsername={HasUser}, hasPassword={HasPass}, hasPrivateKey={HasKey}",
            hasUsername, hasPassword, hasPrivateKey);

        // Prompt for username in-terminal if missing
        if (!hasUsername)
        {
            _logger.LogDebug("[Session] No username configured, showing in-terminal prompt");
            WriteToTerminal("login as: ");
            username = await ReadLineAsync(echo: true, cancellationToken);
            if (string.IsNullOrWhiteSpace(username))
            {
                _logger.LogInformation("[Session] Empty username entered, cancelling connection");
                throw new OperationCanceledException("No username provided.");
            }
            _logger.LogDebug("[Session] Username entered: '{Username}'", username);
        }

        // Build SSH client with appropriate auth methods
        if (hasPrivateKey)
        {
            _logger.LogDebug("[Session] Auth path: PrivateKey ({KeyPath})", options.PrivateKeyPath);
            var keyFile = string.IsNullOrEmpty(options.PrivateKeyPassphrase)
                ? new PrivateKeyFile(options.PrivateKeyPath!)
                : new PrivateKeyFile(options.PrivateKeyPath!, options.PrivateKeyPassphrase);
            _client = new SshClient(options.Host, options.Port, username, [keyFile]);

            _logger.LogInformation("[Session] Connecting to {Host}:{Port} as '{Username}' (key auth)",
                options.Host, options.Port, username);
            await _client.ConnectAsync(cancellationToken);
        }
        else
        {
            // Password-based auth with retry — works for both configured and prompted passwords.
            // Tries PasswordAuth + KeyboardInteractive, covering servers that support either or both.
            const int maxAttempts = 3;
            string? password = hasPassword ? options.Password : null;

            _logger.LogDebug("[Session] Auth path: Password/KeyboardInteractive (hasConfiguredPassword={HasPass}, maxAttempts={Max})",
                hasPassword, maxAttempts);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Prompt for password in-terminal if we don't have one (first time or retry)
                if (string.IsNullOrEmpty(password))
                {
                    _logger.LogDebug("[Session] Prompting for password in terminal (attempt {Attempt}/{Max})", attempt, maxAttempts);
                    WriteToTerminal($"{username}@{options.Host}'s password: ");
                    password = await ReadLineAsync(echo: false, cancellationToken);

                    if (string.IsNullOrEmpty(password))
                    {
                        _logger.LogInformation("[Session] Empty password entered, cancelling connection");
                        throw new OperationCanceledException("No password provided.");
                    }
                    _logger.LogDebug("[Session] Password entered (length={Length})", password.Length);
                }
                else
                {
                    _logger.LogDebug("[Session] Using configured password (attempt {Attempt}/{Max})", attempt, maxAttempts);
                }

                // Build auth methods: password + keyboard-interactive
                var passwordAuth = new PasswordAuthenticationMethod(username, password);
                var kbdAuth = new KeyboardInteractiveAuthenticationMethod(username);

                bool kbdAutoResponded = false;
                var capturedPassword = password; // capture for closure
                kbdAuth.AuthenticationPrompt += (_, e) =>
                {
                    _logger.LogDebug("[Session] KeyboardInteractive AuthenticationPrompt fired, {Count} prompt(s)", e.Prompts.Count);
                    foreach (var prompt in e.Prompts)
                    {
                        _logger.LogDebug("[Session] Prompt: request='{Request}', isEchoed={IsEchoed}", prompt.Request, prompt.IsEchoed);
                        if (!kbdAutoResponded)
                        {
                            _logger.LogDebug("[Session] Auto-responding with current password");
                            prompt.Response = capturedPassword;
                            kbdAutoResponded = true;
                        }
                        else
                        {
                            _logger.LogDebug("[Session] Prompting user in-terminal for additional response");
                            _dispatcher.Invoke(() => WriteToTerminal(prompt.Request));
                            prompt.Response = WaitForLineInput(prompt.IsEchoed);
                            _logger.LogDebug("[Session] User responded (length={Length})", prompt.Response?.Length ?? 0);
                        }
                    }
                };

                var connInfo = new ConnectionInfo(options.Host, options.Port, username, passwordAuth, kbdAuth);
                _client = new SshClient(connInfo);

                _logger.LogInformation("[Session] Connecting to {Host}:{Port} as '{Username}' (attempt {Attempt}/{Max})",
                    options.Host, options.Port, username, attempt, maxAttempts);

                try
                {
                    await _client.ConnectAsync(cancellationToken);
                    _logger.LogDebug("[Session] Authentication succeeded on attempt {Attempt}", attempt);
                    break; // success
                }
                catch (SshAuthenticationException ex) when (attempt < maxAttempts)
                {
                    _logger.LogWarning("[Session] Authentication failed on attempt {Attempt}/{Max}: {Message}",
                        attempt, maxAttempts, ex.Message);
                    WriteToTerminal("Access denied\r\n");
                    _client.Dispose();
                    _client = null;
                    password = null; // force re-prompt on next attempt
                }
            }
        }
        _logger.LogInformation("[Session] SSH authenticated successfully to {Host}:{Port}", options.Host, options.Port);

        _logger.LogDebug("[Session] Creating ShellStream (termType={TermType}, cols={Cols}, rows={Rows}, bufferSize={BufferSize})",
            options.TerminalType, columns, rows, options.BufferSize);
        _stream = _client!.CreateShellStream(
            options.TerminalType,
            (uint)columns,
            (uint)rows,
            0, 0,
            options.BufferSize);

        _cts = new CancellationTokenSource();
        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "SSH-ReadLoop" };
        _readThread.Start();
        _logger.LogDebug("[Session] ReadLoop thread started");

        _logger.LogInformation("[Session] SSH session fully established to {Host}:{Port}", options.Host, options.Port);
        Connected?.Invoke();
        _logger.LogDebug("[Session] Connected event fired");
    }

    /// <summary>
    /// Writes text directly to the virtual terminal (for pre-auth prompts).
    /// Must be called on the UI thread.
    /// </summary>
    private void WriteToTerminal(string text)
    {
        _logger.LogTrace("[Session] WriteToTerminal: '{Text}'", text.Replace("\r\n", "\\r\\n"));
        var bytes = Encoding.UTF8.GetBytes(text);
        _dataConsumer.Push(bytes);
        TerminalUpdated?.Invoke();
    }

    /// <summary>
    /// Async readline for pre-auth username prompt. Runs on UI thread,
    /// awaits user input via the input capture system.
    /// </summary>
    private Task<string> ReadLineAsync(bool echo, CancellationToken ct)
    {
        _logger.LogDebug("[Session] ReadLineAsync started (echo={Echo})", echo);
        _inputBuffer.Clear();
        _captureEcho = echo;
        _inputCancelled = false;
        _inputTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _isCapturingInput = true;
        InteractivePromptStarted?.Invoke();

        ct.Register(() =>
        {
            _logger.LogDebug("[Session] ReadLineAsync cancellation triggered");
            _isCapturingInput = false;
            _inputTcs?.TrySetCanceled();
        });

        return _inputTcs.Task;
    }

    /// <summary>
    /// Blocking readline for keyboard-interactive auth prompts.
    /// Called from the SSH.NET auth background thread.
    /// </summary>
    private string WaitForLineInput(bool echo)
    {
        _logger.LogDebug("[Session] WaitForLineInput started (echo={Echo}), setting up capture on UI thread", echo);
        _dispatcher.Invoke(() =>
        {
            _inputBuffer.Clear();
            _captureEcho = echo;
            _inputCancelled = false;
            _inputReadyEvent.Reset();
            _isCapturingInput = true;
        });

        _dispatcher.Invoke(() => InteractivePromptStarted?.Invoke());
        _logger.LogDebug("[Session] WaitForLineInput blocking on ManualResetEvent");
        _inputReadyEvent.Wait();

        if (_inputCancelled)
        {
            _logger.LogInformation("[Session] WaitForLineInput cancelled by user (Ctrl+C)");
            throw new OperationCanceledException("User cancelled input.");
        }

        _logger.LogDebug("[Session] WaitForLineInput completed (resultLength={Length})", _blockingInputResult?.Length ?? 0);
        return _blockingInputResult ?? string.Empty;
    }

    /// <summary>
    /// Processes captured keystrokes during pre-auth / interactive auth.
    /// Called from the UI thread via Write().
    /// </summary>
    private void HandleCapturedInput(byte[] data)
    {
        var text = Encoding.UTF8.GetString(data);
        foreach (var ch in text)
        {
            if (ch == '\r' || ch == '\n')
            {
                _logger.LogDebug("[Session] CapturedInput: Enter pressed, completing input (length={Length})", _inputBuffer.Length);
                WriteToTerminal("\r\n");
                var result = _inputBuffer.ToString();
                _isCapturingInput = false;

                // Complete whichever wait mechanism is active
                _inputTcs?.TrySetResult(result);
                _blockingInputResult = result;
                _inputReadyEvent.Set();
                return;
            }
            else if (ch == '\x03') // Ctrl+C — cancel
            {
                _logger.LogDebug("[Session] CapturedInput: Ctrl+C pressed, cancelling input");
                WriteToTerminal("^C\r\n");
                _isCapturingInput = false;
                _inputCancelled = true;
                _inputTcs?.TrySetCanceled();
                _blockingInputResult = null;
                _inputReadyEvent.Set();
                return;
            }
            else if (ch == '\x7f' || ch == '\b') // Backspace
            {
                if (_inputBuffer.Length > 0)
                {
                    _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
                    if (_captureEcho)
                        WriteToTerminal("\b \b");
                }
            }
            else if (ch >= ' ') // Printable characters
            {
                _inputBuffer.Append(ch);
                if (_captureEcho)
                    WriteToTerminal(ch.ToString());
            }
        }
    }

    private void ReadLoop()
    {
        _logger.LogDebug("[Session] ReadLoop started on thread {ThreadId}", Environment.CurrentManagedThreadId);
        var buffer = new byte[4096];
        try
        {
            while (_cts is { IsCancellationRequested: false } && _stream != null)
            {
                // Check DataAvailable first to avoid blocking on Read() after disconnect.
                // ShellStream.Read() blocks when no data is available, so we poll instead.
                if (!_stream.DataAvailable)
                {
                    if (_client?.IsConnected != true)
                    {
                        _logger.LogDebug("[Session] ReadLoop: client disconnected (no data), exiting");
                        break;
                    }
                    Thread.Sleep(10);
                    continue;
                }

                int bytesRead;
                try
                {
                    bytesRead = _stream.Read(buffer, 0, buffer.Length);
                }
                catch (ObjectDisposedException)
                {
                    _logger.LogDebug("[Session] ReadLoop: stream disposed, exiting");
                    break;
                }

                if (bytesRead <= 0)
                    continue;

                _logger.LogTrace("[Session] ReadLoop: received {BytesRead} bytes", bytesRead);
                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);

                _dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        _dataConsumer.Push(data);
                        TerminalUpdated?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        ErrorOccurred?.Invoke(ex);
                    }
                });
            }
        }
        catch (Exception ex) when (ex is not ThreadAbortException)
        {
            _logger.LogError(ex, "[Session] ReadLoop error");
            _dispatcher.InvokeAsync(() => ErrorOccurred?.Invoke(ex));
        }
        finally
        {
            _logger.LogInformation("[Session] ReadLoop exiting, firing Disconnected event");
            _dispatcher.InvokeAsync(() => Disconnected?.Invoke());
        }
    }

    public void Write(byte[] data)
    {
        if (_isCapturingInput)
        {
            HandleCapturedInput(data);
            return;
        }

        try
        {
            _stream?.Write(data, 0, data.Length);
            _stream?.Flush();
        }
        catch (ObjectDisposedException)
        {
            // Stream disposed during disconnect — expected, not an error
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }
    }

    public void Write(string text)
    {
        Write(Encoding.UTF8.GetBytes(text));
    }

    public void ResizeTerminal(int columns, int rows)
    {
        Columns = columns;
        Rows = rows;
        _terminal.ResizeView(columns, rows);
    }

    public void SendWindowChangeRequest(int columns, int rows)
    {
        try
        {
            _stream?.ChangeWindowSize((uint)columns, (uint)rows, 0, 0);
        }
        catch (ObjectDisposedException)
        {
            // Stream disposed during disconnect — expected
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }
    }

    public void Disconnect()
    {
        _logger.LogDebug("[Session] Disconnect called");

        // Cancel any pending input capture
        _isCapturingInput = false;
        _inputCancelled = true;
        _inputTcs?.TrySetCanceled();
        _inputReadyEvent.Set();

        _cts?.Cancel();
        _logger.LogDebug("[Session] CancellationToken cancelled");

        if (_readThread?.IsAlive == true)
        {
            _logger.LogDebug("[Session] Waiting for ReadLoop thread to join");
            _readThread.Join(TimeSpan.FromSeconds(2));
        }

        _stream?.Dispose();
        _stream = null;
        _logger.LogDebug("[Session] ShellStream disposed");

        if (_client?.IsConnected == true)
        {
            _logger.LogDebug("[Session] Disconnecting SshClient");
            _client.Disconnect();
        }
        _client?.Dispose();
        _client = null;
        _logger.LogDebug("[Session] SshClient disposed");

        _cts?.Dispose();
        _cts = null;
        _readThread = null;
        _logger.LogDebug("[Session] Disconnect complete");
    }

    public void Dispose()
    {
        Disconnect();
        _inputReadyEvent.Dispose();
    }
}
