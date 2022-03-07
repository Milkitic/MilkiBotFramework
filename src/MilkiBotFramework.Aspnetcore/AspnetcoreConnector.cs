using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Aspnetcore;

public class AspnetcoreConnector : IConnector
{
    public event Func<string, Task>? RawMessageReceived;

    protected readonly IWebSocketConnector? WebSocketConnector;
    private readonly ILogger<AspnetcoreConnector> _logger;
    private readonly WebApplication _webApplication;

    private readonly List<TaskCompletionSource> _messageWaiters = new();
    private WebSocketMessageManager? _manager;
    private WebSocket? _webSocket;

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
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public Encoding? Encoding { get; set; }

    public async Task ConnectAsync()
    {
        if (ConnectionType == ConnectionType.Websocket && WebSocketConnector != null)
        {
            WebSocketConnector.RawMessageReceived += (s) =>
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
        else if (ConnectionType == ConnectionType.ReverseWebsocket)
        {
            _manager = new WebSocketMessageManager(_logger,
                () => MessageTimeout,
                async message =>
                {
                    if (_webSocket == null) return;
                    var buffer = Encoding.UTF8.GetBytes(message);
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                },
                RawMessageReceived,
                TryGetStateByMessage
            );
        }

        await _webApplication.StartAsync();
    }

    public async Task DisconnectAsync()
    {
        await _webApplication.StopAsync();
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        if (ConnectionType == ConnectionType.ReverseWebsocket)
        {
            if (_webSocket == null)
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

        if (WebSocketConnector != null)
        {
            return await WebSocketConnector.SendMessageAsync(message, state);
        }

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

    private async Task WsMessageReceiveLoop(WebSocket webSocket)
    {
        //const int maxLen = 1024 * 1024 * 10;
        var buffer = new byte[1024 * 64 + 1];
        var receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue)
        {
            if (receiveResult.MessageType != WebSocketMessageType.Text)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType,
                    "Only support text message.",
                    CancellationToken.None);
                return;
            }

            Memory<byte> actualBytes;
            if (!receiveResult.EndOfMessage)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig,
                    "Text message size reaches max limit: " + (buffer.Length - 1),
                    CancellationToken.None);
                return;

                #region backup

                //// receive by buffer sequence(rwlock) if not text
                //await using var ms = new MemoryStream();
                //ms.Write(buffer);

                //while (receiveResult.Count == buffer.Length)
                //{
                //    receiveResult = await webSocket.ReceiveAsync(
                //        new ArraySegment<byte>(buffer), CancellationToken.None);

                //    if (receiveResult.CloseStatus.HasValue)
                //    {
                //        await webSocket.CloseAsync(
                //            receiveResult.CloseStatus.Value,
                //            receiveResult.CloseStatusDescription,
                //            CancellationToken.None);
                //        return;
                //    }

                //    ms.Write(buffer.AsSpan(0, receiveResult.Count));
                //    if (ms.Length <= maxLen) continue;

                //    await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig,
                //        "Message size reaches max limit: " + maxLen,
                //        CancellationToken.None);
                //    return;
                //}

                //actualBytes = ms.ToArray();

                #endregion
            }
            else
            {
                actualBytes = buffer.AsMemory(0, receiveResult.Count);
            }

            var message = Encoding.Default.GetString(actualBytes.Span);
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
                new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None);
    }

    protected virtual bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        state = null;
        return false;
    }
}