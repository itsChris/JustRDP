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

    public VirtualTerminalController Terminal => _terminal;
    public bool IsConnected => _client?.IsConnected == true;
    public int Columns { get; private set; } = 80;
    public int Rows { get; private set; } = 24;
    public int CursorRow => _terminal.CursorState.CurrentRow;
    public int CursorColumn => _terminal.CursorState.CurrentColumn;

    public event Action? TerminalUpdated;
    public event Action? Connected;
    public event Action? Disconnected;
    public event Action<Exception>? ErrorOccurred;

    public TerminalSession(Dispatcher dispatcher, ILogger<TerminalSession> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _terminal = new VirtualTerminalController();
        _terminal.ResizeView(80, 24);
        _dataConsumer = new DataConsumer(_terminal);
    }

    public async Task ConnectAsync(TerminalOptions options, int columns, int rows, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new ArgumentException("Host is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.Username))
            throw new ArgumentException("Username is required.", nameof(options));
        if (options.Port is < 1 or > 65535)
            throw new ArgumentException("Port must be between 1 and 65535.", nameof(options));
        if (string.IsNullOrEmpty(options.Password) && string.IsNullOrEmpty(options.PrivateKeyPath))
            throw new ArgumentException("Either Password or PrivateKeyPath is required.", nameof(options));

        Columns = columns;
        Rows = rows;
        _terminal.ResizeView(columns, rows);

        // Create SSH client with appropriate auth
        if (!string.IsNullOrEmpty(options.PrivateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(options.PrivateKeyPassphrase)
                ? new PrivateKeyFile(options.PrivateKeyPath)
                : new PrivateKeyFile(options.PrivateKeyPath, options.PrivateKeyPassphrase);
            _client = new SshClient(options.Host, options.Port, options.Username, [keyFile]);
        }
        else
        {
            _client = new SshClient(options.Host, options.Port, options.Username, options.Password ?? string.Empty);
        }

        _logger.LogInformation("Connecting to {Host}:{Port} as {Username}", options.Host, options.Port, options.Username);

        await _client.ConnectAsync(cancellationToken);

        _stream = _client.CreateShellStream(
            options.TerminalType,
            (uint)columns,
            (uint)rows,
            0, 0,
            options.BufferSize);

        _cts = new CancellationTokenSource();
        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "SSH-ReadLoop" };
        _readThread.Start();

        _logger.LogInformation("SSH session established to {Host}:{Port}", options.Host, options.Port);
        Connected?.Invoke();
    }

    private void ReadLoop()
    {
        var buffer = new byte[4096];
        try
        {
            while (_cts is { IsCancellationRequested: false } && _stream != null)
            {
                int bytesRead;
                try
                {
                    bytesRead = _stream.Read(buffer, 0, buffer.Length);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (bytesRead <= 0)
                {
                    Thread.Sleep(10);
                    continue;
                }

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
            _logger.LogError(ex, "SSH read loop error");
            _dispatcher.InvokeAsync(() => ErrorOccurred?.Invoke(ex));
        }
        finally
        {
            _logger.LogInformation("SSH session disconnected");
            _dispatcher.InvokeAsync(() => Disconnected?.Invoke());
        }
    }

    public void Write(byte[] data)
    {
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
        _cts?.Cancel();

        if (_readThread?.IsAlive == true)
            _readThread.Join(TimeSpan.FromSeconds(2));

        _stream?.Dispose();
        _stream = null;

        if (_client?.IsConnected == true)
            _client.Disconnect();
        _client?.Dispose();
        _client = null;

        _cts?.Dispose();
        _cts = null;
        _readThread = null;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
