using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;

namespace MilkiBotFramework.Aspnetcore;

public class ReverseWebsocketMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AspnetcoreConnector _connector;

    public ReverseWebsocketMiddleware(RequestDelegate next, IConnector connector)
    {
        _next = next;
        _connector = (AspnetcoreConnector)connector;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals(_connector.BindingPath, StringComparison.OrdinalIgnoreCase))
        {
            if (context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await _connector.OnWebSocketOpen(webSocket);
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
}