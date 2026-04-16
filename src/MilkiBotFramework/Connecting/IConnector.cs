namespace MilkiBotFramework.Connecting;

public interface IConnector : IConnectorConfigurable
{
    event Func<InboundMessage, Task>? MessageReceived;
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
    Task<string> SendMessageAsync(string message, string state);
}