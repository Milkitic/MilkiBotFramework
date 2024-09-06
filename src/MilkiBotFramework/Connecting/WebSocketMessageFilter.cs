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

    private TaskCompletionSource _tcs = new();
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

    public async Task<T?> FilterMessageAsync<T>(Func<AsyncWsMessage, T?> filter)
    {
        while (!_cancellationTokenSource.IsCancellationRequested)
        {
            try
            {
                await _tcs.Task;
            }
            catch (TaskCanceledException)
            {
                return default;
            }

            using (await _asyncLock.LockAsync(_cancellationTokenSource.Token))
            {
                _tcs = new TaskCompletionSource();
            }

            while (_queue.TryDequeue(out var message))
            {
                var asyncWsMessage = new AsyncWsMessage(message);
                var result = filter(asyncWsMessage);
                if (asyncWsMessage.IsHandled) return result;
            }
        }

        return default;
    }

    private async Task PushMessageAsync(string message)
    {
        _queue.Enqueue(message);
        using (await _asyncLock.LockAsync(_cancellationTokenSource.Token))
        {
            _tcs.TrySetResult();
        }
    }

    private async Task WebSocketClientConnector_RawMessageReceived(string message)
    {
        await PushMessageAsync(message);
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

        _tcs.TrySetCanceled();
        _asyncLock.Dispose();
        _queue.Clear();
    }
}

public class AsyncWsMessage
{
    public AsyncWsMessage(string message)
    {
        Message = message;
    }

    public string Message { get; set; }
    public bool IsHandled { get; set; }
}