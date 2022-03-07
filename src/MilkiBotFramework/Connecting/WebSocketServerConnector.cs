using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Fleck;
using Microsoft.Extensions.Logging;

namespace MilkiBotFramework.Connecting;

public abstract class WebSocketServerConnector : IWebSocketConnector, IDisposable, IAsyncDisposable
{
    public event Func<string, Task>? RawMessageReceived;

    private readonly ILogger<WebSocketServerConnector> _logger;

    private IWebSocketConnection? _socket;
    private WebSocketServer? _server;
    private readonly ConcurrentDictionary<string, WebsocketRequestSession> _sessions = new();
    private readonly List<TaskCompletionSource> _messageWaiters = new();
    private readonly WebSocketMessageSessionManager _sessionManager;

    public WebSocketServerConnector(ILogger<WebSocketServerConnector> logger)
    {
        _logger = logger;
        _sessionManager = new WebSocketMessageSessionManager(logger,
            () => MessageTimeout,
            async message =>
            {
                if (_socket != null) await _socket.Send(message);
            },
            RawMessageReceived,
            TryGetStateByMessage
        );
    }

    public ConnectionType ConnectionType { get; set; }
    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 消息超时时间。
    /// 对于一些长消息超时的情况，请适量增大此值。
    /// </summary>
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public Encoding? Encoding { get; set; }

    public Task ConnectAsync()
    {
        _server = new WebSocketServer(BindingPath);
        FleckLog.Level = Fleck.LogLevel.Error;
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

                if (_messageWaiters.Count > 0)
                {
                    foreach (var taskCompletionSource in _messageWaiters.ToArray())
                    {
                        taskCompletionSource.SetResult();
                    }
                }

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
                await _sessionManager.InvokeMessageReceive(message);
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
        {
            var connectionWaiter = new TaskCompletionSource();
            _messageWaiters.Add(connectionWaiter);
            using var cts1 = new CancellationTokenSource(ConnectionTimeout);
            cts1.Token.Register(() =>
            {
                try
                {
                    connectionWaiter.SetCanceled();
                    _logger.LogWarning($"Connection is forced to time out after {ConnectionTimeout.Seconds} seconds.");
                }
                catch
                {
                    // ignored
                }
            });
            try
            {
                await connectionWaiter.Task;
            }
            catch
            {
                throw new ArgumentNullException(nameof(_socket), "There is no available websocket connection.");
            }
            finally
            {
                _messageWaiters.Remove(connectionWaiter);
            }
        }

        return await _sessionManager.SendMessageAsync(message, state);
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
}