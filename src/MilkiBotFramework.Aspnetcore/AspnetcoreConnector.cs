using System.Text;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Aspnetcore
{
    public class AspnetcoreConnector : IConnector
    {
        private readonly WebSocketClientConnector? _webSocketClientConnector;
        private readonly WebApplication _webApplication;

        public AspnetcoreConnector(WebApplication webApplication, WebSocketClientConnector? webSocketClientConnector)
        {
            _webSocketClientConnector = webSocketClientConnector;
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
            if (_webSocketClientConnector != null)
            {
                try
                {
                    _webSocketClientConnector.ConnectAsync().Wait(3000);
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

        public Task DisconnectAsync()
        {
            throw new NotImplementedException();
        }

        public async Task<string> SendMessageAsync(string message, string state)
        {
            if (_webSocketClientConnector != null)
            {
                return await _webSocketClientConnector.SendMessageAsync(message, state);
            }

            var helper = HttpHelper.Default;
            return helper.HttpPostJson(TargetUri + "/" + state, message);
        }
    }
}
