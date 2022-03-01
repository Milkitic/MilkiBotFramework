using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Connecting
{
    internal class StandardIoConnector : IConnector, IDisposable
    {
        public event Func<string, Task>? RawMessageReceived;
        private readonly ILogger _logger;
        private readonly AsyncLock _singletonIoLock = new();
        private bool _enable;

        public StandardIoConnector(ILogger logger)
        {
            _logger = logger;
            _logger.LogInformation("StandardIoConnector for debugging usage!");
        }

        public ConnectionType ConnectionType { get; set; }
        public string TargetUri { get; set; }
        public string? BindingPath { get; set; }
        public TimeSpan ConnectionTimeout { get; set; }
        public TimeSpan MessageTimeout { get; set; }
        public Encoding Encoding { get; set; }

        public Task ConnectAsync()
        {
            _enable = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            _enable = false;
            return Task.CompletedTask;
        }

        public async Task<string> SendMessageAsync(string message, string state)
        {
            if (!_enable) throw new Exception("StandardIo is not ready. Try to connect before sending message.");
            using (await _singletonIoLock.LockAsync())
            {
                Console.Write("Received request: \r\n" + message + "\r\nEnter single-lined response:");
                var response = await Console.In.ReadLineAsync();
                return response ?? "";
            }
        }

        public void Dispose()
        {
            _singletonIoLock.Dispose();
        }
    }
}
