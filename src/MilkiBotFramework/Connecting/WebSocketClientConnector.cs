using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Utils;
using Websocket.Client;

namespace MilkiBotFramework.Connecting;

public abstract class WebSocketClientConnector : IConnector, IAsyncDisposable
{
    public event Func<string, Task>? RawMessageReceived;

    private readonly ILogger _logger;
    private readonly AsyncLock _asyncLock = new();
    private WebsocketClient? _client;
    private readonly ConcurrentDictionary<string, WebsocketRequestSession> _sessions = new();
    private bool _isConnected = false;

    public WebSocketClientConnector(ILogger<WebSocketClientConnector> logger)
    {
        _logger = logger;
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

    public Encoding Encoding { get; set; } = Encoding.UTF8;

    public async Task ConnectAsync()
    {
        using (await _asyncLock.LockAsync())
        {
            if (_client is { IsStarted: true })
                return;
        }

        await DisconnectAsync();

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
        _client.MessageReceived.Subscribe(async msg => await OnMessageReceived(msg));

        try
        {
            _logger.LogInformation($"Starting managed websocket connection to {TargetUri}...");
            await _client.Start();
            //_logger.LogInformation($"Connected to websocket server.");
        }
        catch (Exception e)
        {
            _logger.LogError(e.Message);
        }
    }

    public async Task DisconnectAsync()
    {
        using (await _asyncLock.LockAsync())
        {
            if (_client is not { IsStarted: true })
                return;

            if (_client != null)
            {
                await _client.StopOrFail(WebSocketCloseStatus.Empty, null);
                _client = null;
            }
        }
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        if (_client == null)
            throw new ArgumentNullException(nameof(_client),
                "WebsocketClient is not ready. Try to connect before sending message.");
        //if (!_isConnected)
        //    throw new Exception("WebsocketClient is not connected.");

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
        _client.Send(message);
        try
        {
            await tcs.Task;
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

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync();
        _asyncLock.Dispose();
    }

    protected virtual bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        state = null;
        return false;
    }

    private async Task OnMessageReceived(ResponseMessage message)
    {
        if (message.MessageType != WebSocketMessageType.Text || string.IsNullOrWhiteSpace(message.Text))
            return;
        await OnMessageReceivedCore(message.Text);
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