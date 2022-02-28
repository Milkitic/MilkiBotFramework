using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using MilkiBotFramework.Dispatching;

namespace MilkiBotFramework.Aspnetcore
{
    [ApiController]
    [Route("connector")]
    public class ConnectorController : ControllerBase
    {
        private readonly IDispatcher _dispatcher;
        private readonly ILogger<ConnectorController> _logger;

        public ConnectorController(IDispatcher dispatcher,
            ILogger<ConnectorController> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;
        }

        [HttpPost]
        public async Task Post()
        {
            // Allows using several time the stream in ASP.Net Core
            HttpContext.Request.EnableBuffering();
            using var reader = new StreamReader(HttpContext.Request.Body, Encoding.UTF8, true, 1024, true);
            string bodyStr = await reader.ReadToEndAsync();
            _logger.LogDebug("!!!POST STR: " + bodyStr);
            await _dispatcher.InvokeRawMessageReceived(bodyStr);
        }

        [HttpGet("reverse-ws")]
        public async Task Get()
        {
            if (HttpContext.WebSockets.IsWebSocketRequest)
            {
                using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
                await DoAsyncWorks(webSocket);
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        private static async Task DoAsyncWorks(WebSocket webSocket)
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
}
