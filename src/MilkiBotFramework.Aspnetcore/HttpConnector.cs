using System.Text;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Aspnetcore
{
    public class HttpConnector : IConnector
    {
        public string ServerUri { get; set; }
        public TimeSpan ConnectionTimeout { get; set; }
        public TimeSpan MessageTimeout { get; set; }
        public Encoding Encoding { get; set; }
        public event Func<string, Task>? RawMessageReceived;
        public Task ConnectAsync()
        {
            throw new NotImplementedException();
        }

        public Task DisconnectAsync()
        {
            throw new NotImplementedException();
        }

        public Task<string> SendMessageAsync(string message, string state)
        {
            throw new NotImplementedException();
        }
    }
}
