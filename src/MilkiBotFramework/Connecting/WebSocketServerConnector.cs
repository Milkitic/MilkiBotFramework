using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Fleck;
using Microsoft.Extensions.Logging;
using LogLevel = Fleck.LogLevel;

namespace MilkiBotFramework.Connecting;

public abstract class WebSocketServerConnector : IWebSocketConnector, IDisposable, IAsyncDisposable
{
    public event Func<string, Task>? RawMessageReceived;

    private readonly ILogger<WebSocketServerConnector> _logger;

    private IWebSocketConnection? _socket;
    private WebSocketServer? _server;
    private readonly ConcurrentDictionary<string, WebsocketRequestSession> _sessions = new();

    public WebSocketServerConnector(ILogger<WebSocketServerConnector> logger)
    {
        _logger = logger;
    }

    public ConnectionType ConnectionType { get; set; }
    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public TimeSpan ConnectionTimeout { get; set; }

    /// <summary>
    /// 消息超时时间。
    /// 对于一些长消息超时的情况，请适量增大此值。
    /// </summary>
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public Encoding? Encoding { get; set; }

    public Task ConnectAsync()
    {
        _server = new WebSocketServer(BindingPath);
        FleckLog.Level = LogLevel.Error;
        _server.Start(socket =>
        {
            socket.OnOpen = () =>
            {
                if (_socket != null)
                {
                    socket.Close();
                    _logger.LogInformation("Force to close the connection because there is already a connection.");
                    return;
                }

                _socket = socket;
                _logger.LogInformation("Websocket client connected.");
            };
            socket.OnClose = () =>
            {
                if (_socket != socket) return;
                _socket = null;
                _logger.LogInformation("Websocket client disconnected.");
            };
            socket.OnMessage = async message =>
            {
                await OnMessageReceivedCore(message);
            };
            socket.OnError = exception =>
            {
                _logger.LogWarning($"Error occurs in websocket thread: {exception.Message}");
                if (_socket != socket) return;
                _socket.Close();
                _socket = null;
            };
        });
        _logger.LogInformation($"Starting managed websocket server on {TargetUri}...");
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _socket?.Close();
        _server?.Dispose();
        return Task.CompletedTask;
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        if (_socket == null)
            throw new ArgumentNullException(nameof(_socket), "There is no available websocket connection.");

        var tcs = new TaskCompletionSource();
        using var cts = new CancellationTokenSource(MessageTimeout);
        cts.Token.Register(() =>
        {
            try
            {
                tcs.SetCanceled();
            }
            catch
            {
                _logger.LogWarning($"Message is forced to time out after {MessageTimeout.Seconds} seconds.");
            }
        });
        var sessionObj = new WebsocketRequestSession(tcs);
        _sessions.TryAdd(state, sessionObj);
        await _socket.Send(message);
        try
        {
            await tcs.Task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            throw new Exception("Timed out for receiving response from websocket server.", ex);
        }
        finally
        {
            _sessions.TryRemove(state, out _);
        }

        if (sessionObj.Response == null) throw new NullReferenceException();
        return sessionObj.Response;
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
    }

    protected virtual bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        state = null;
        return false;
    }

    private Task OnMessageReceivedCore(string msg)
    {
        var hasState = TryGetStateByMessage(msg, out var state);
        if (!hasState || string.IsNullOrEmpty(state))
        {
            RawMessageReceived?.Invoke(msg);
            return Task.CompletedTask;
        }

        if (_sessions.TryGetValue(state, out var sessionObj))
        {
            sessionObj.Response = msg;
            try
            {
                sessionObj.TaskCompletionSource.SetResult();
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning($"Rollback to raw message: response received but the task has been canceled due to the timeout setting.");
            }
        }
        else
        {
            _logger.LogWarning($"Rollback to raw message due to unknown response state: {state}.");
            RawMessageReceived?.Invoke(msg);
        }

        return Task.CompletedTask;
    }
}