using System.Text;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Aspnetcore;

public class AspnetcoreConnector : IConnector
{
    protected readonly IWebSocketConnector? WebSocketConnector;
    private readonly WebApplication _webApplication;

    public AspnetcoreConnector(IWebSocketConnector? webSocketConnector,
        WebApplication webApplication)
    {
        WebSocketConnector = webSocketConnector;
        _webApplication = webApplication;
    }

    public ConnectionType ConnectionType { get; set; }
    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public TimeSpan ConnectionTimeout { get; set; }
    public TimeSpan MessageTimeout { get; set; }
    public Encoding? Encoding { get; set; }
    public event Func<string, Task>? RawMessageReceived;
    public async Task ConnectAsync()
    {
        if (WebSocketConnector != null)
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

        await _webApplication.StartAsync();
    }

    public async Task DisconnectAsync()
    {
        await _webApplication.StopAsync();
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        if (WebSocketConnector != null)
        {
            return await WebSocketConnector.SendMessageAsync(message, state);
        }

        throw new NotSupportedException();
    }
}