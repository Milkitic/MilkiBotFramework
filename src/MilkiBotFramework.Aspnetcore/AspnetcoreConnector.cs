using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Aspnetcore;

public class AspnetcoreConnector : IConnector
{
    public event Func<InboundMessage, Task>? MessageReceived;

    protected readonly IWebSocketConnector? WebSocketConnector;
    private readonly ILogger<AspnetcoreConnector> _logger;
    private readonly WebApplication _webApplication;
    private readonly AsyncLock _connectionsLock = new();

    private readonly List<TaskCompletionSource> _messageWaiters = [];
    private readonly Dictionary<string, List<TaskCompletionSource>> _accountMessageWaiters =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReverseWebSocketConnection> _reverseWebSocketConnections =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _accountConnectionMapping = new(StringComparer.OrdinalIgnoreCase);

    private const int WsMaxLen = 1024 * 1024 * 10;

    public AspnetcoreConnector(IWebSocketConnector? webSocketConnector,
        ILogger<AspnetcoreConnector> logger,
        WebApplication webApplication)
    {
        WebSocketConnector = webSocketConnector;
        _logger = logger;
        _webApplication = webApplication;
    }

    public ConnectionType ConnectionType { get; set; }
    public string? TargetUri { get; set; }
    public virtual string? BindingPath { get; set; }
    public TimeSpan ErrorReconnectTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public Encoding? Encoding { get; set; }

    public virtual async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (ConnectionType == ConnectionType.WebSocket && WebSocketConnector != null)
        {
            await ConnectInnerWsClient();
        }
        else if (ConnectionType == ConnectionType.ReverseWebSocket)
        {
            ConnectReverseWs();
        }

        await _webApplication.StartAsync(cancellationToken);
    }

    public virtual async Task DisconnectAsync()
    {
        ReverseWebSocketConnection[] reverseWebSocketConnections;
        using (await _connectionsLock.LockAsync())
        {
            reverseWebSocketConnections = _reverseWebSocketConnections.Values.ToArray();
            _reverseWebSocketConnections.Clear();
            _accountConnectionMapping.Clear();
            _messageWaiters.Clear();
            _accountMessageWaiters.Clear();
        }

        foreach (var reverseWebSocketConnection in reverseWebSocketConnections)
        {
            await CloseSocketAsync(reverseWebSocketConnection.Socket,
                WebSocketCloseStatus.NormalClosure,
                "Server closed.");
            reverseWebSocketConnection.Socket.Dispose();
        }

        await _webApplication.StopAsync();
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        return await SendMessageAsync(message, state, null);
    }

    public async Task<string> SendMessageAsync(string message, string state, string? accountId)
    {
        if (ConnectionType == ConnectionType.ReverseWebSocket)
            return await SendWsMessage(message, state, accountId);
        if (WebSocketConnector != null)
            return await WebSocketConnector.SendMessageAsync(message, state);
        throw new NotSupportedException();
    }

    protected async Task PublishInboundMessageAsync(InboundMessage inboundMessage)
    {
        if (MessageReceived != null) await MessageReceived.Invoke(inboundMessage);
    }

    internal async Task OnWebSocketOpen(WebSocket webSocket, IHeaderDictionary? headers = null)
    {
        var connectionId = Guid.NewGuid().ToString("N");
        ReverseWebSocketConnection reverseWebSocketConnection;
        ReverseWebSocketConnection? replacedConnection = null;
        string? initialAccountId;

        using (await _connectionsLock.LockAsync())
        {
            if (!AllowMultipleReverseWebSocketConnections && _reverseWebSocketConnections.Count > 0)
            {
                await CloseSocketAsync(webSocket,
                    WebSocketCloseStatus.EndpointUnavailable,
                    "There is already a connection for this server.");
                _logger.LogInformation("Force to close the connection because there is already a connection.");
                return;
            }

            initialAccountId = ResolveReverseWebSocketAccountId(headers);
            reverseWebSocketConnection = new ReverseWebSocketConnection(connectionId, webSocket, _logger,
                () => MessageTimeout,
                inboundMessage => MessageReceived?.Invoke(inboundMessage) ?? Task.CompletedTask,
                TryGetStateByMessage);
            _reverseWebSocketConnections[connectionId] = reverseWebSocketConnection;

            if (!string.IsNullOrWhiteSpace(initialAccountId))
            {
                replacedConnection = RegisterReverseWebSocketAccountNoLock(reverseWebSocketConnection, initialAccountId);
            }

            SignalConnectionWaitersNoLock(initialAccountId);
        }

        if (replacedConnection != null)
        {
            await CloseSocketAsync(replacedConnection.Socket,
                WebSocketCloseStatus.PolicyViolation,
                "This account has been reconnected by another websocket.");
        }

        try
        {
            await WsMessageReceiveLoop(reverseWebSocketConnection);
        }
        catch (Exception ex)
        {
            _logger.LogError("WebSocketServer loop error: " + ex.Message);
        }
        finally
        {
            webSocket.Dispose();
            using (await _connectionsLock.LockAsync())
            {
                RemoveReverseWebSocketConnectionNoLock(reverseWebSocketConnection);
            }
        }
    }

    protected virtual bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        state = null;
        return false;
    }

    protected virtual bool AllowMultipleReverseWebSocketConnections => false;

    protected virtual string? ResolveReverseWebSocketAccountId(IHeaderDictionary? headers)
    {
        return null;
    }

    protected virtual string? ResolveReverseWebSocketAccountId(string message)
    {
        return null;
    }

    private async Task WsMessageReceiveLoop(ReverseWebSocketConnection reverseWebSocketConnection)
    {
        var webSocket = reverseWebSocketConnection.Socket;
        var wsBuffer = new byte[1024 * 8];
        var receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(wsBuffer), CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue)
        {
            if (receiveResult.MessageType != WebSocketMessageType.Text)
            {
                await CloseSocketAsync(webSocket,
                    WebSocketCloseStatus.InvalidMessageType,
                    "Only support text message.");
                return;
            }

            string message;
            if (!receiveResult.EndOfMessage)
            {
                await using var ms = new MemoryStream();
                ms.Write(wsBuffer, 0, receiveResult.Count);

                while (!receiveResult.EndOfMessage)
                {
                    receiveResult = await webSocket.ReceiveAsync(
                        new ArraySegment<byte>(wsBuffer), CancellationToken.None);

                    if (receiveResult.CloseStatus.HasValue)
                    {
                        await CloseSocketAsync(webSocket,
                            receiveResult.CloseStatus.Value,
                            receiveResult.CloseStatusDescription);
                        return;
                    }

                    ms.Write(wsBuffer.AsSpan(0, receiveResult.Count));
                    if (ms.Length <= WsMaxLen) continue;

                    await CloseSocketAsync(webSocket,
                        WebSocketCloseStatus.MessageTooBig,
                        "Message size reaches max limit: " + WsMaxLen);
                    return;
                }

                ms.Position = 0;
                using var sr = new StreamReader(ms, Encoding.Default);
                message = await sr.ReadToEndAsync();
            }
            else
            {
                var actualBytes = wsBuffer.AsMemory(0, receiveResult.Count);
                message = Encoding.Default.GetString(actualBytes.Span);
            }

            try
            {
                await BindReverseWebSocketAccountAsync(reverseWebSocketConnection, message);
                await reverseWebSocketConnection.SessionManager.InvokeMessageReceive(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurs while executing dispatcher");
            }

            receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(wsBuffer), CancellationToken.None);
        }

        await CloseSocketAsync(webSocket,
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription);
    }

    private async Task ConnectInnerWsClient()
    {
        WebSocketConnector!.MessageReceived += inboundMessage =>
        {
            if (MessageReceived != null) return MessageReceived(inboundMessage);
            return Task.CompletedTask;
        };

        try
        {
            using var cts = new CancellationTokenSource(3000);
            await WebSocketConnector.ConnectAsync(cts.Token);
        }
        catch (Exception ex)
        {
            if (ex is not TaskCanceledException &&
                ex.InnerException is not TaskCanceledException)
            {
                throw;
            }
            // ignored
        }
    }

    private void ConnectReverseWs()
    {
        // Reverse websocket connections are accepted lazily and managed per active socket.
    }

    private async Task<string> SendWsMessage(string message, string state, string? accountId)
    {
        var reverseWebSocketConnection = await GetReverseWebSocketConnectionAsync(accountId);
        return await reverseWebSocketConnection.SessionManager.SendMessageAsync(message, state);
    }

    private async Task<ReverseWebSocketConnection> GetReverseWebSocketConnectionAsync(string? accountId)
    {
        if (TryResolveReverseWebSocketConnection(accountId, out var reverseWebSocketConnection, out var isAmbiguous))
        {
            return reverseWebSocketConnection;
        }

        if (isAmbiguous)
        {
            throw new InvalidOperationException("Multiple reverse websocket connections are active. Specify an account id when sending.");
        }

        var connectionWaiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using (await _connectionsLock.LockAsync())
        {
            if (TryResolveReverseWebSocketConnectionNoLock(accountId, out reverseWebSocketConnection, out isAmbiguous))
            {
                return reverseWebSocketConnection;
            }

            if (isAmbiguous)
            {
                throw new InvalidOperationException("Multiple reverse websocket connections are active. Specify an account id when sending.");
            }

            if (string.IsNullOrWhiteSpace(accountId))
            {
                _messageWaiters.Add(connectionWaiter);
            }
            else
            {
                if (!_accountMessageWaiters.TryGetValue(accountId, out var accountWaiters))
                {
                    accountWaiters = [];
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
            using (await _connectionsLock.LockAsync())
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

        if (TryResolveReverseWebSocketConnection(accountId, out reverseWebSocketConnection, out isAmbiguous))
        {
            return reverseWebSocketConnection;
        }

        if (isAmbiguous)
        {
            throw new InvalidOperationException("Multiple reverse websocket connections are active. Specify an account id when sending.");
        }

        throw new ArgumentNullException(nameof(accountId), "There is no available websocket connection.");
    }

    private bool TryResolveReverseWebSocketConnection(string? accountId,
        [NotNullWhen(true)] out ReverseWebSocketConnection? reverseWebSocketConnection,
        out bool isAmbiguous)
    {
        using var _ = _connectionsLock.Lock();
        return TryResolveReverseWebSocketConnectionNoLock(accountId, out reverseWebSocketConnection, out isAmbiguous);
    }

    private bool TryResolveReverseWebSocketConnectionNoLock(string? accountId,
        [NotNullWhen(true)] out ReverseWebSocketConnection? reverseWebSocketConnection,
        out bool isAmbiguous)
    {
        if (!string.IsNullOrWhiteSpace(accountId))
        {
            isAmbiguous = false;
            if (_accountConnectionMapping.TryGetValue(accountId, out var connectionId) &&
                _reverseWebSocketConnections.TryGetValue(connectionId, out reverseWebSocketConnection))
            {
                return true;
            }

            reverseWebSocketConnection = null;
            return false;
        }

        if (_reverseWebSocketConnections.Count == 1)
        {
            isAmbiguous = false;
            reverseWebSocketConnection = _reverseWebSocketConnections.Values.First();
            return true;
        }

        isAmbiguous = _reverseWebSocketConnections.Count > 1;
        reverseWebSocketConnection = null;
        return false;
    }

    private async Task BindReverseWebSocketAccountAsync(ReverseWebSocketConnection reverseWebSocketConnection,
        string message)
    {
        var accountId = ResolveReverseWebSocketAccountId(message);
        if (string.IsNullOrWhiteSpace(accountId))
        {
            return;
        }

        ReverseWebSocketConnection? replacedConnection;
        using (await _connectionsLock.LockAsync())
        {
            if (!_reverseWebSocketConnections.ContainsKey(reverseWebSocketConnection.ConnectionId))
            {
                return;
            }

            replacedConnection = RegisterReverseWebSocketAccountNoLock(reverseWebSocketConnection, accountId);
            SignalConnectionWaitersNoLock(accountId);
        }

        if (replacedConnection != null)
        {
            await CloseSocketAsync(replacedConnection.Socket,
                WebSocketCloseStatus.PolicyViolation,
                "This account has been reconnected by another websocket.");
        }
    }

    private ReverseWebSocketConnection? RegisterReverseWebSocketAccountNoLock(
        ReverseWebSocketConnection reverseWebSocketConnection,
        string accountId)
    {
        if (!string.IsNullOrWhiteSpace(reverseWebSocketConnection.AccountId) &&
            !string.Equals(reverseWebSocketConnection.AccountId, accountId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Reverse websocket connection {ConnectionId} changed account binding from {OldAccountId} to {AccountId}.",
                reverseWebSocketConnection.ConnectionId,
                reverseWebSocketConnection.AccountId,
                accountId);
        }

        reverseWebSocketConnection.AccountId = accountId;
        if (_accountConnectionMapping.TryGetValue(accountId, out var existingConnectionId) &&
            !string.Equals(existingConnectionId, reverseWebSocketConnection.ConnectionId, StringComparison.OrdinalIgnoreCase) &&
            _reverseWebSocketConnections.TryGetValue(existingConnectionId, out var replacedConnection))
        {
            _accountConnectionMapping[accountId] = reverseWebSocketConnection.ConnectionId;
            return replacedConnection;
        }

        _accountConnectionMapping[accountId] = reverseWebSocketConnection.ConnectionId;
        return null;
    }

    private void RemoveReverseWebSocketConnectionNoLock(ReverseWebSocketConnection reverseWebSocketConnection)
    {
        _reverseWebSocketConnections.Remove(reverseWebSocketConnection.ConnectionId);
        if (!string.IsNullOrWhiteSpace(reverseWebSocketConnection.AccountId) &&
            _accountConnectionMapping.TryGetValue(reverseWebSocketConnection.AccountId, out var mappedConnectionId) &&
            string.Equals(mappedConnectionId, reverseWebSocketConnection.ConnectionId, StringComparison.OrdinalIgnoreCase))
        {
            _accountConnectionMapping.Remove(reverseWebSocketConnection.AccountId);
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

    private static async Task CloseSocketAsync(WebSocket webSocket,
        WebSocketCloseStatus closeStatus,
        string? statusDescription)
    {
        if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await webSocket.CloseAsync(closeStatus, statusDescription, CancellationToken.None);
        }
    }

    private sealed class ReverseWebSocketConnection
    {
        public ReverseWebSocketConnection(string connectionId,
            WebSocket socket,
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
        public WebSocket Socket { get; }
        public string? AccountId { get; set; }
        public AsyncLock SendLock { get; } = new();
        public WebSocketMessageSessionManager SessionManager { get; }

        private async Task SendAsync(string message)
        {
            using (await SendLock.LockAsync())
            {
                if (Socket.State != WebSocketState.Open)
                {
                    throw new WebSocketException("Websocket is not open.");
                }

                var buffer = Encoding.UTF8.GetBytes(message);
                await Socket.SendAsync(new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None);
            }
        }
    }
}