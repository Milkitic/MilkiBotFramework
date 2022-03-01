using System.Net.WebSockets;
using System.Text;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Aspnetcore;

public class ReverseWebsocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _path;

    public ReverseWebsocketMiddleware(RequestDelegate next, IConnector connector)
    {
        _next = next;
        _path = connector.BindingPath!;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals(_path, StringComparison.OrdinalIgnoreCase))
        {
            if (context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await DoAsyncWorks(webSocket);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            }
        }
        else
        {
            await _next(context);
        }
    }

    private async Task DoAsyncWorks(WebSocket webSocket)
    {
        //const int maxLen = 1024 * 1024 * 10;
        var buffer = new byte[1024 * 64 + 1];
        var receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue)
        {
            Memory<byte> actualBytes;
            if (receiveResult.Count == buffer.Length)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.MessageTooBig,
                    "Text message size reaches max limit: " + (buffer.Length - 1),
                    CancellationToken.None);
                return;

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
            }
            else
            {
                actualBytes = buffer.AsMemory(0, receiveResult.Count);
            }

            if (receiveResult.MessageType != WebSocketMessageType.Text)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidMessageType,
                    "Only support text message.",
                    CancellationToken.None);
                return;
            }

            var message = Encoding.Default.GetString(actualBytes.Span);
            // send by buffer sequence(rwlock)
            //await webSocket.SendAsync(
            //    new ArraySegment<byte>(buffer, 0, receiveResult.Count),
            //    receiveResult.MessageType,
            //    receiveResult.EndOfMessage,
            //    CancellationToken.None);

            receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None);
    }
}