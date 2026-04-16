using Microsoft.Extensions.Logging;

namespace MilkiBotFramework.Connecting;

public sealed class CompositeConnector : IConnector
{
    private readonly IPlatformConnector[] _connectors;
    private readonly ILogger<CompositeConnector> _logger;

    public CompositeConnector(IEnumerable<IPlatformConnector> connectors, ILogger<CompositeConnector> logger)
    {
        _connectors = connectors.ToArray();
        _logger = logger;
        foreach (var connector in _connectors)
        {
            connector.MessageReceived += inboundMessage => MessageReceived?.Invoke(inboundMessage) ?? Task.CompletedTask;
        }
    }

    public event Func<InboundMessage, Task>? MessageReceived;

    public ConnectionType ConnectionType
    {
        get => ConnectionType.WebSocket;
        set { }
    }

    public string? TargetUri
    {
        get => null;
        set { }
    }

    public string? BindingPath
    {
        get => null;
        set { }
    }

    public TimeSpan ErrorReconnectTimeout
    {
        get => TimeSpan.Zero;
        set { }
    }

    public TimeSpan MessageTimeout
    {
        get => TimeSpan.Zero;
        set { }
    }

    public System.Text.Encoding? Encoding
    {
        get => null;
        set { }
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        foreach (var connector in _connectors)
        {
            _logger.LogInformation("Connecting platform {PlatformId}", connector.PlatformId);
            await connector.ConnectAsync(cancellationToken);
        }
    }

    public async Task DisconnectAsync()
    {
        foreach (var connector in _connectors.Reverse())
        {
            await connector.DisconnectAsync();
        }
    }

    public Task<string> SendMessageAsync(string message, string state)
    {
        throw new NotSupportedException("CompositeConnector does not support direct send. Use IMessageApi instead.");
    }
}