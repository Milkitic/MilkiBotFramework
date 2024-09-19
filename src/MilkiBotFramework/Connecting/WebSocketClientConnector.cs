using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Utils;
using Websocket.Client;

namespace MilkiBotFramework.Connecting;

public abstract class WebSocketClientConnector : IWebSocketConnector, IDisposable, IAsyncDisposable
{
    public event Action<ReconnectionInfo>? ReconnectionHappened;
    public event Action<DisconnectionInfo>? DisconnectionHappened;
    public event Func<string, Task>? RawMessageReceived;

    protected WebsocketClient? Client;
    private readonly ILogger<WebSocketClientConnector> _logger;

    private readonly AsyncLock _asyncLock = new();
    private bool _isConnected;
    private readonly WebSocketMessageSessionManager _sessionManager;

    public WebSocketClientConnector(ILogger<WebSocketClientConnector> logger)
    {
        _logger = logger;
        _sessionManager = new WebSocketMessageSessionManager(logger,
            () => MessageTimeout, message =>
            {
                Client?.Send(message);
                return Task.CompletedTask;
            },
            async message =>
            {
                if (RawMessageReceived != null) await RawMessageReceived.Invoke(message);
            },
            TryGetStateByMessage
        );
    }

    public ConnectionType ConnectionType { get; set; }
    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public TimeSpan ErrorReconnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    //public TimeSpan ReconnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan ReconnectTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// 消息超时时间。
    /// 对于一些长消息超时的情况，请适量增大此值。
    /// </summary>
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public Encoding? Encoding { get; set; } = Encoding.UTF8;

    public virtual async Task ConnectAsync()
    {
        using (await _asyncLock.LockAsync().ConfigureAwait(false))
        {
            if (Client is { IsStarted: true })
                return;
        }

        await DisconnectAsync().ConfigureAwait(false);

        if (TargetUri == null) throw new ArgumentNullException(nameof(TargetUri));

        Client = new WebsocketClient(new Uri(TargetUri))
        {
            ErrorReconnectTimeout = ErrorReconnectTimeout,
            MessageEncoding = Encoding,
            ReconnectTimeout = ReconnectTimeout
        };

        Client.ReconnectionHappened.Subscribe(ReconnectionHappened);
        Client.DisconnectionHappened.Subscribe(DisconnectionHappened);
        Client.MessageReceived.Subscribe(MessageReceived);

        try
        {
            _logger.LogInformation($"Starting managed websocket connection to {TargetUri}...");
            await Client.Start().ConfigureAwait(false);
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
        }

        return;

        async void ReconnectionHappened(ReconnectionInfo info)
        {
            _isConnected = true;
            if (info.Type == ReconnectionType.Initial)
                _logger.LogInformation("Connected to websocket server.");
            else if (info.Type != ReconnectionType.NoMessageReceived && info.Type != ReconnectionType.Lost) _logger.LogInformation("Reconnected to websocket server.");
            if (info.Type != ReconnectionType.Lost)
            {
                try
                {
                    this.ReconnectionHappened?.Invoke(info);
                    await OnReconnectionHappened(info);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error occurs while calling {nameof(this.ReconnectionHappened)} callback.");
                }
            }
        }

        async void DisconnectionHappened(DisconnectionInfo info)
        {
            var action = _isConnected ? "Disconnected from" : "Cannot connect to";
            _isConnected = false;
            if (info.Exception != null)
                _logger.LogWarning($"{action} the websocket server: {info.Exception.Message}");
            else
                _logger.LogWarning($"{action} the websocket server: {info.Type}");
            try
            {
                this.DisconnectionHappened?.Invoke(info);
                await OnDisconnectionHappened(info);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error occurs while calling {nameof(this.DisconnectionHappened)} callback.");
            }
        }

        async void MessageReceived(ResponseMessage msg)
        {
            await OnMessageReceived(msg);
        }
    }

    public virtual async Task DisconnectAsync()
    {
        using (await _asyncLock.LockAsync().ConfigureAwait(false))
        {
            if (Client is not { IsStarted: true })
                return;

            if (Client != null)
            {
                await Client.StopOrFail(WebSocketCloseStatus.Empty, null!).ConfigureAwait(false);
                Client.Dispose();
                Client = null;
            }
        }
    }

    protected virtual ValueTask OnReconnectionHappened(ReconnectionInfo reconnectionInfo)
    {
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask OnDisconnectionHappened(DisconnectionInfo disconnectionInfo)
    {
        return ValueTask.CompletedTask;
    }


    public WebSocketMessageFilter SendMessage(string message)
    {
        if (Client == null)
        {
            throw new ArgumentNullException(nameof(Client),
                "WebsocketClient is not ready. Try to connect before sending message.");
        }

        var filter = new WebSocketMessageFilter(_logger, this);
        Client?.Send(message);
        return filter;
    }

    public Task<string> SendMessageAsync(string message, string state)
    {
        if (Client == null)
        {
            throw new ArgumentNullException(nameof(Client),
                "WebsocketClient is not ready. Try to connect before sending message.");
        }

        return _sessionManager.SendMessageAsync(message, state);
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        _asyncLock.Dispose();
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
        _asyncLock.Dispose();
    }

    protected virtual bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        state = null;
        return false;
    }

    private Task OnMessageReceived(ResponseMessage message)
    {
        if (message.MessageType != WebSocketMessageType.Text || string.IsNullOrWhiteSpace(message.Text))
            return Task.CompletedTask;
        return _sessionManager.InvokeMessageReceive(message.Text);
    }
}