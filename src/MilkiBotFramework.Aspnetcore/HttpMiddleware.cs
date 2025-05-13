using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;

namespace MilkiBotFramework.Aspnetcore;

public class HttpMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<HttpMiddleware> _logger;
    private readonly string _path;

    public HttpMiddleware(RequestDelegate next,
        IConnector connector,
        IDispatcher dispatcher,
        ILogger<HttpMiddleware> logger)
    {
        _next = next;
        _dispatcher = dispatcher;
        _logger = logger;
        _path = connector.BindingPath!;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals(_path, StringComparison.OrdinalIgnoreCase))
        {
            if (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                // context.Request.EnableBuffering(); // context using several time the stream in ASP.Net Core
                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true);
                var bodyStr = await reader.ReadToEndAsync();
                //_logger.LogDebug("!!!POST STR: " + bodyStr);
                try
                {
                    await _dispatcher.InvokeRawMessageReceived(bodyStr);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurs while executing dispatcher");
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