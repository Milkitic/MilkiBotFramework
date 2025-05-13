using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Aspnetcore;

public class AspnetcoreConnector : IConnector
{
    public event Func<string, Task>? RawMessageReceived;

    protected readonly IWebSocketConnector? WebSocketConnector;
    private readonly ILogger<AspnetcoreConnector> _logger;
    private readonly WebApplication _webApplication;

    private readonly AsyncLock _ioLock = new();

    private readonly List<TaskCompletionSource> _messageWaiters = new();
    private WebSocketMessageSessionManager? _manager;
    private WebSocket? _webSocket;

    private const int WsMaxLen = 1024 * 1024 * 10;
    private readonly byte[] _wsBuffer = new byte[1024 * 8];

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
    public string? BindingPath { get; set; }
    public TimeSpan ErrorReconnectTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public Encoding? Encoding { get; set; }

    public async Task ConnectAsync()
    {
        if (ConnectionType == ConnectionType.WebSocket && WebSocketConnector != null)
        {
            ConnectInnerWsClient();
        }
        else if (ConnectionType == ConnectionType.ReverseWebSocket)
        {
            ConnectReverseWs();
        }

        await _webApplication.StartAsync();
    }

    public async Task DisconnectAsync()
    {
        if (_webSocket != null)
        {
            await _webSocket.CloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "Server closed.",
                CancellationToken.None);
        }

        await _webApplication.StopAsync();
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        if (ConnectionType == ConnectionType.ReverseWebSocket)
            return await SendWsMessage(message, state);
        if (WebSocketConnector != null)
            return await WebSocketConnector.SendMessageAsync(message, state);
        throw new NotSupportedException();
    }

    internal async Task OnWebSocketOpen(WebSocket webSocket)
    {
        if (_webSocket != null)
        {
            await webSocket.CloseAsync(
                WebSocketCloseStatus.EndpointUnavailable,
                "There is already a connection for this server.",
                CancellationToken.None);
            _logger.LogInformation("Force to close the connection because there is already a connection.");
            return;
        }

        _webSocket = webSocket;

        if (_messageWaiters.Count > 0)
        {
            foreach (var taskCompletionSource in _messageWaiters.ToArray())
            {
                taskCompletionSource.SetResult();
            }
        }

        try
        {
            await WsMessageReceiveLoop(webSocket);
        }
        catch (Exception ex)
        {
            _logger.LogError("WebSocketServer loop error: " + ex.Message);
        }

        webSocket.Dispose();
        _webSocket = null;
    }

    protected virtual bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        state = null;
        return false;
    }

    private async Task WsMessageReceiveLoop(WebSocket webSocket)
    {
        var receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(_wsBuffer), CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue)
        {
            if (receiveResult.MessageType != WebSocketMessageType.Text)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType,
                    "Only support text message.",
                    CancellationToken.None);
                return;
            }

            string message;
            if (!receiveResult.EndOfMessage)
            {
                using (await _ioLock.LockAsync())
                {
                    await using var ms = new MemoryStream();
                    ms.Write(_wsBuffer);

                    while (!receiveResult.EndOfMessage)
                    {
                        receiveResult = await webSocket.ReceiveAsync(
                            new ArraySegment<byte>(_wsBuffer), CancellationToken.None);

                        if (receiveResult.CloseStatus.HasValue)
                        {
                            await webSocket.CloseAsync(
                                receiveResult.CloseStatus.Value,
                                receiveResult.CloseStatusDescription,
                                CancellationToken.None);
                            return;
                        }

                        ms.Write(_wsBuffer.AsSpan(0, receiveResult.Count));
                        if (ms.Length <= WsMaxLen) continue;

                        await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig,
                            "Message size reaches max limit: " + WsMaxLen,
                            CancellationToken.None);
                        return;
                    }

                    ms.Position = 0;
                    using var sr = new StreamReader(ms, Encoding.Default);
                    message = await sr.ReadToEndAsync();
                }
            }
            else
            {
                var actualBytes = _wsBuffer.AsMemory(0, receiveResult.Count);
                message = Encoding.Default.GetString(actualBytes.Span);
            }

            try
            {
                if (_manager != null) await _manager.InvokeMessageReceive(message);
                else throw new ArgumentException("WebSocketMessageManager is null.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurs while executing dispatcher");
            }

            receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(_wsBuffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None);
    }

    private void ConnectInnerWsClient()
    {
        WebSocketConnector!.RawMessageReceived += (s) =>
        {
            if (RawMessageReceived != null) return RawMessageReceived(s);
            return Task.CompletedTask;
        };

        try
        {
            WebSocketConnector.ConnectAsync().Wait(3000);
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
        _manager = new WebSocketMessageSessionManager(_logger,
            () => MessageTimeout,
            async message =>
            {
                using (await _ioLock.LockAsync())
                {
                    if (_webSocket == null) return;
                    var buffer = Encoding.UTF8.GetBytes(message);
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
            },
            RawMessageReceived,
            TryGetStateByMessage
        );
    }

    private async Task<string> SendWsMessage(string message, string state)
    {
        if (_webSocket == null)
        {
            var connectionWaiter = new TaskCompletionSource();
            _messageWaiters.Add(connectionWaiter);
            using var cts1 = new CancellationTokenSource(ErrorReconnectTimeout);
            cts1.Token.Register(() =>
            {
                try
                {
                    connectionWaiter.SetCanceled();
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
                throw new ArgumentNullException(nameof(_webSocket), "There is no available websocket connection.");
            }
            finally
            {
                _messageWaiters.Remove(connectionWaiter);
            }
        }

        if (_manager != null) return await _manager.SendMessageAsync(message, state);
        else throw new ArgumentException("WebSocketMessageManager is null.");
    }
}