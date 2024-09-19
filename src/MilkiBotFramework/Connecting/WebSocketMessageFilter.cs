using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Utils;
using Websocket.Client;

namespace MilkiBotFramework.Connecting;

public class WebSocketMessageFilter : IDisposable
{
    private readonly ILogger _logger;
    private readonly WebSocketClientConnector _webSocketClientConnector;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly AsyncLock _asyncLock = new();
    private readonly AutoResetEvent _autoResetEvent = new(false);

    private bool _isDisposed;
    private bool _disconnected;

    public WebSocketMessageFilter(ILogger logger, WebSocketClientConnector webSocketClientConnector)
    {
        _logger = logger;
        _webSocketClientConnector = webSocketClientConnector;
        _cancellationTokenSource = new CancellationTokenSource(webSocketClientConnector.MessageTimeout);
        _cancellationTokenSource.Token.Register(() =>
        {
            if (_isDisposed)
            {
                if (_disconnected)
                {
                    _logger.LogWarning($"Message is forced expired because the connection is closed.");
                }
            }
            else
            {
                var seconds = webSocketClientConnector.MessageTimeout.Seconds;
                _logger.LogWarning($"Message is forced expired by {seconds} seconds.");
            }
        });
        webSocketClientConnector.DisconnectionHappened += WebSocketClientConnector_DisconnectionHappened;
        webSocketClientConnector.RawMessageReceived += WebSocketClientConnector_RawMessageReceived;
    }

    public async Task<T?> FilterMessageAsync<T>(Func<WebSocketAsyncMessage, T?> filter)
    {
        try
        {
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                await _autoResetEvent.WaitOneAsync(_cancellationTokenSource.Token);
                while (_queue.TryDequeue(out var message))
                {
                    var asyncWsMessage = new WebSocketAsyncMessage(message);
                    var result = filter(asyncWsMessage);
                    if (asyncWsMessage.IsHandled) return result;
                }
            }
        }
        catch (TaskCanceledException)
        {
            return default;
        }
        catch (OperationCanceledException)
        {
            return default;
        }

        return default;
    }

    private void EnqueueMessage(string message)
    {
        _queue.Enqueue(message);
        _autoResetEvent.Set();
    }

    private Task WebSocketClientConnector_RawMessageReceived(string message)
    {
        EnqueueMessage(message);
        return Task.CompletedTask;
    }

    private void WebSocketClientConnector_DisconnectionHappened(DisconnectionInfo obj)
    {
        _disconnected = true;
        Dispose();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _webSocketClientConnector.RawMessageReceived -= WebSocketClientConnector_RawMessageReceived;
        _webSocketClientConnector.DisconnectionHappened -= WebSocketClientConnector_DisconnectionHappened;

        try
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }

        _asyncLock.Dispose();
        _queue.Clear();
    }
}