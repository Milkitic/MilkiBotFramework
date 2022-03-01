using System.Text;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Aspnetcore;

public class AspnetcoreConnector : IConnector
{
    protected readonly WebSocketClientConnector? WebSocketClientConnector;
    private readonly WebApplication _webApplication;

    public AspnetcoreConnector(WebApplication webApplication,
        WebSocketClientConnector? webSocketClientConnector)
    {
        WebSocketClientConnector = webSocketClientConnector;
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
        if (WebSocketClientConnector != null)
        {
            WebSocketClientConnector.RawMessageReceived += (s) =>
            {
                if (RawMessageReceived != null) return RawMessageReceived(s);
                return Task.CompletedTask;
            };

            try
            {
                WebSocketClientConnector.ConnectAsync().Wait(3000);
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
        if (WebSocketClientConnector != null)
        {
            return await WebSocketClientConnector.SendMessageAsync(message, state);
        }

        throw new NotSupportedException();
    }
}