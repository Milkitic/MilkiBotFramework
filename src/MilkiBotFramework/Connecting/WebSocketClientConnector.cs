using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Utils;
using Websocket.Client;

namespace MilkiBotFramework.Connecting;

public abstract class WebSocketClientConnector : IWebSocketConnector, IDisposable, IAsyncDisposable
{
    public event Func<string, Task>? RawMessageReceived;

    private readonly ILogger _logger;

    private readonly AsyncLock _asyncLock = new();
    private WebsocketClient? _client;
    private bool _isConnected;
    private readonly WebSocketMessageSessionManager _sessionManager;

    public WebSocketClientConnector(ILogger<WebSocketClientConnector> logger)
    {
        _logger = logger;
        _sessionManager = new WebSocketMessageSessionManager(logger,
            () => MessageTimeout, message =>
            {
                _client?.Send(message);
                return Task.CompletedTask;
            },
            RawMessageReceived,
            TryGetStateByMessage
        );
    }

    public ConnectionType ConnectionType { get; set; }
    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// 消息超时时间。
    /// 对于一些长消息超时的情况，请适量增大此值。
    /// </summary>
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public Encoding? Encoding { get; set; } = Encoding.UTF8;

    public async Task ConnectAsync()
    {
        using (await _asyncLock.LockAsync().ConfigureAwait(false))
        {
            if (_client is { IsStarted: true })
                return;
        }

        await DisconnectAsync().ConfigureAwait(false);

        if (TargetUri == null) throw new ArgumentNullException(nameof(TargetUri));

        _client = new WebsocketClient(new Uri(TargetUri))
        {
            ErrorReconnectTimeout = ConnectionTimeout,
            MessageEncoding = Encoding,
        };
        _client.ReconnectionHappened.Subscribe(info =>
        {
            _isConnected = true;
            if (info.Type == ReconnectionType.Initial)
                _logger.LogInformation("Connected to websocket server.");
            else
                _logger.LogInformation("Reconnected to websocket server.");
        });
        _client.DisconnectionHappened.Subscribe(info =>
        {
            var action = _isConnected ? "Disconnected from" : "Cannot connect to";
            _isConnected = false;
            if (info.Exception != null)
                _logger.LogWarning($"{action} the websocket server: {info.Exception.Message}");
            else
                _logger.LogWarning($"{action} the websocket server: {info.Type}");
        });
        // ReSharper disable once AsyncVoidLambda
        _client.MessageReceived.Subscribe(async msg => await OnMessageReceived(msg));

        try
        {
            _logger.LogInformation($"Starting managed websocket connection to {TargetUri}...");
            await _client.Start().ConfigureAwait(false);
            //_logger.LogInformation($"Connected to websocket server.");
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
        }
    }

    public async Task DisconnectAsync()
    {
        using (await _asyncLock.LockAsync().ConfigureAwait(false))
        {
            if (_client is not { IsStarted: true })
                return;

            if (_client != null)
            {
                await _client.StopOrFail(WebSocketCloseStatus.Empty, null).ConfigureAwait(false);
                _client.Dispose();
                _client = null;
            }
        }
    }

    public Task<string> SendMessageAsync(string message, string state)
    {
        if (_client == null)
            throw new ArgumentNullException(nameof(_client),
                "WebsocketClient is not ready. Try to connect before sending message.");

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