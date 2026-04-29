using System.Diagnostics.CodeAnalysis;
using System.Text;
using Fleck;
using Microsoft.Extensions.Logging;

namespace MilkiBotFramework.Connecting;

public abstract class WebSocketServerConnector : IWebSocketConnector, IDisposable, IAsyncDisposable
{
    public event Func<InboundMessage, Task>? MessageReceived;

    private readonly ILogger<WebSocketServerConnector> _logger;

    private WebSocketServer? _server;
    private readonly object _connectionsLock = new();
    private readonly List<TaskCompletionSource> _messageWaiters = new();
    private readonly Dictionary<string, List<TaskCompletionSource>> _accountMessageWaiters =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ServerWebSocketConnection> _connections =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _accountConnectionMapping =
        new(StringComparer.OrdinalIgnoreCase);

    public WebSocketServerConnector(ILogger<WebSocketServerConnector> logger)
    {
        _logger = logger;
    }

    public ConnectionType ConnectionType { get; set; }
    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public TimeSpan ErrorReconnectTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// 消息超时时间。
    /// 对于一些长消息超时的情况，请适量增大此值。
    /// </summary>
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public Encoding? Encoding { get; set; }

    public Task ConnectAsync(CancellationToken cancellationToken)
    {
        _server = new WebSocketServer(BindingPath);
        FleckLog.Level = Fleck.LogLevel.Error;
        _server.Start(socket =>
        {
            ServerWebSocketConnection? connection = null;
            socket.OnOpen = () =>
            {
                ServerWebSocketConnection? replacedConnection = null;
                lock (_connectionsLock)
                {
                    if (!AllowMultipleConnections && _connections.Count > 0)
                    {
                        socket.Close();
                        _logger.LogInformation("Force to close the connection because there is already a connection.");
                        return;
                    }

                    connection = new ServerWebSocketConnection(Guid.NewGuid().ToString("N"),
                        socket,
                        _logger,
                        () => MessageTimeout,
                        inboundMessage => MessageReceived?.Invoke(inboundMessage) ?? Task.CompletedTask,
                        TryGetStateByMessage);
                    _connections[connection.ConnectionId] = connection;

                    var initialAccountId = ResolveConnectionAccountId(socket.ConnectionInfo.Headers);
                    if (!string.IsNullOrWhiteSpace(initialAccountId))
                    {
                        replacedConnection = RegisterConnectionAccountNoLock(connection, initialAccountId);
                    }

                    SignalConnectionWaitersNoLock(initialAccountId);
                }

                if (replacedConnection != null)
                {
                    replacedConnection.Socket.Close();
                }

                _logger.LogInformation("WebSocket client connected.");
            };
            socket.OnClose = () =>
            {
                if (connection == null) return;
                lock (_connectionsLock)
                {
                    RemoveConnectionNoLock(connection);
                }

                _logger.LogInformation("WebSocket client disconnected.");
            };
            // ReSharper disable once AsyncVoidLambda
            socket.OnMessage = async message =>
            {
                if (connection == null) return;

                await BindConnectionAccountAsync(connection, message);
                await connection.SessionManager.InvokeMessageReceive(message);
            };
            socket.OnError = exception =>
            {
                _logger.LogWarning($"Error occurs in websocket thread: {exception.Message}");
                if (connection == null) return;

                socket.Close();
                lock (_connectionsLock)
                {
                    RemoveConnectionNoLock(connection);
                }
            };
        });
        _logger.LogInformation($"Starting managed websocket server on {TargetUri}...");
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        lock (_connectionsLock)
        {
            foreach (var connection in _connections.Values.ToArray())
            {
                connection.Socket.Close();
            }

            _connections.Clear();
            _accountConnectionMapping.Clear();
            _messageWaiters.Clear();
            _accountMessageWaiters.Clear();
        }

        _server?.Dispose();
        return Task.CompletedTask;
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        return await SendMessageAsync(message, state, null);
    }

    public async Task<string> SendMessageAsync(string message, string state, string? accountId)
    {
        var connection = await GetConnectionAsync(accountId);
        return await connection.SessionManager.SendMessageAsync(message, state);
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

    protected virtual bool AllowMultipleConnections => false;

    protected virtual string? ResolveConnectionAccountId(string message)
    {
        return null;
    }

    protected virtual string? ResolveConnectionAccountId(IDictionary<string, string>? headers)
    {
        return null;
    }

    private async Task<ServerWebSocketConnection> GetConnectionAsync(string? accountId)
    {
        if (TryResolveConnection(accountId, out var connection, out var isAmbiguous))
        {
            return connection;
        }

        if (isAmbiguous)
        {
            throw new InvalidOperationException("Multiple websocket connections are active. Specify an account id when sending.");
        }

        var connectionWaiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_connectionsLock)
        {
            if (TryResolveConnectionNoLock(accountId, out connection, out isAmbiguous))
            {
                return connection;
            }

            if (isAmbiguous)
            {
                throw new InvalidOperationException("Multiple websocket connections are active. Specify an account id when sending.");
            }

            if (string.IsNullOrWhiteSpace(accountId))
            {
                _messageWaiters.Add(connectionWaiter);
            }
            else
            {
                if (!_accountMessageWaiters.TryGetValue(accountId, out var accountWaiters))
                {
                    accountWaiters = new List<TaskCompletionSource>();
                    _accountMessageWaiters[accountId] = accountWaiters;
                }

                accountWaiters.Add(connectionWaiter);
            }
        }

        using var cts = new CancellationTokenSource(ErrorReconnectTimeout);
        cts.Token.Register(() =>
        {
            try
            {
                connectionWaiter.TrySetCanceled();
                _logger.LogWarning($"Connection is forced to time out after {ErrorReconnectTimeout.Seconds} seconds.");
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
            throw new ArgumentNullException(nameof(accountId), "There is no available websocket connection.");
        }
        finally
        {
            lock (_connectionsLock)
            {
                if (string.IsNullOrWhiteSpace(accountId))
                {
                    _messageWaiters.Remove(connectionWaiter);
                }
                else if (_accountMessageWaiters.TryGetValue(accountId, out var accountWaiters))
                {
                    accountWaiters.Remove(connectionWaiter);
                    if (accountWaiters.Count == 0)
                    {
                        _accountMessageWaiters.Remove(accountId);
                    }
                }
            }
        }

        if (TryResolveConnection(accountId, out connection, out isAmbiguous))
        {
            return connection;
        }

        if (isAmbiguous)
        {
            throw new InvalidOperationException("Multiple websocket connections are active. Specify an account id when sending.");
        }

        throw new ArgumentNullException(nameof(accountId), "There is no available websocket connection.");
    }

    private bool TryResolveConnection(string? accountId,
        [NotNullWhen(true)] out ServerWebSocketConnection? connection,
        out bool isAmbiguous)
    {
        lock (_connectionsLock)
        {
            return TryResolveConnectionNoLock(accountId, out connection, out isAmbiguous);
        }
    }

    private bool TryResolveConnectionNoLock(string? accountId,
        [NotNullWhen(true)] out ServerWebSocketConnection? connection,
        out bool isAmbiguous)
    {
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            isAmbiguous = false;
            if (_accountConnectionMapping.TryGetValue(accountId, out var connectionId) &&
                _connections.TryGetValue(connectionId, out connection))
            {
                return true;
            }

            connection = null;
            return false;
        }

        if (_connections.Count == 1)
        {
            isAmbiguous = false;
            connection = _connections.Values.First();
            return true;
        }

        isAmbiguous = _connections.Count > 1;
        connection = null;
        return false;
    }

    private Task BindConnectionAccountAsync(ServerWebSocketConnection connection, string message)
    {
        var accountId = ResolveConnectionAccountId(message);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return Task.CompletedTask;
        }

        ServerWebSocketConnection? replacedConnection;
        lock (_connectionsLock)
        {
            if (!_connections.ContainsKey(connection.ConnectionId))
            {
                return Task.CompletedTask;
            }

            replacedConnection = RegisterConnectionAccountNoLock(connection, accountId);
            SignalConnectionWaitersNoLock(accountId);
        }

        if (replacedConnection != null)
        {
            replacedConnection.Socket.Close();
        }

        return Task.CompletedTask;
    }

    private ServerWebSocketConnection? RegisterConnectionAccountNoLock(ServerWebSocketConnection connection,
        string accountId)
    {
        if (!string.IsNullOrWhiteSpace(connection.AccountId) &&
            !string.Equals(connection.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("WebSocket connection {ConnectionId} changed account binding from {OldAccountId} to {AccountId}.",
                connection.ConnectionId,
                connection.AccountId,
                accountId);
        }

        connection.AccountId = accountId;
        if (_accountConnectionMapping.TryGetValue(accountId, out var existingConnectionId) &&
            !string.Equals(existingConnectionId, connection.ConnectionId, StringComparison.OrdinalIgnoreCase) &&
            _connections.TryGetValue(existingConnectionId, out var replacedConnection))
        {
            _accountConnectionMapping[accountId] = connection.ConnectionId;
            return replacedConnection;
        }

        _accountConnectionMapping[accountId] = connection.ConnectionId;
        return null;
    }

    private void RemoveConnectionNoLock(ServerWebSocketConnection connection)
    {
        _connections.Remove(connection.ConnectionId);
        if (!string.IsNullOrWhiteSpace(connection.AccountId) &&
            _accountConnectionMapping.TryGetValue(connection.AccountId, out var mappedConnectionId) &&
            string.Equals(mappedConnectionId, connection.ConnectionId, StringComparison.OrdinalIgnoreCase))
        {
            _accountConnectionMapping.Remove(connection.AccountId);
        }
    }

    private void SignalConnectionWaitersNoLock(string? accountId)
    {
        foreach (var taskCompletionSource in _messageWaiters.ToArray())
        {
            taskCompletionSource.TrySetResult();
        }

        if (string.IsNullOrWhiteSpace(accountId) ||
            !_accountMessageWaiters.TryGetValue(accountId, out var accountWaiters))
        {
            return;
        }

        foreach (var taskCompletionSource in accountWaiters.ToArray())
        {
            taskCompletionSource.TrySetResult();
        }
    }

    private sealed class ServerWebSocketConnection
    {
        public ServerWebSocketConnection(string connectionId,
            IWebSocketConnection socket,
            ILogger logger,
            Func<TimeSpan> getMessageTimeout,
            Func<InboundMessage, Task> messageReceived,
            WebSocketMessageSessionManager.TryGetStateByMessageDelegate tryGetStateByMessage)
        {
            ConnectionId = connectionId;
            Socket = socket;
            SessionManager = new WebSocketMessageSessionManager(logger,
                getMessageTimeout,
                SendAsync,
                messageReceived,
                tryGetStateByMessage);
        }

        public string ConnectionId { get; }
        public IWebSocketConnection Socket { get; }
        public string? AccountId { get; set; }
        public WebSocketMessageSessionManager SessionManager { get; }

        private async Task SendAsync(string message)
        {
            await Socket.Send(message);
        }
    }
}
