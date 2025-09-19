namespace MilkiBotFramework.Connecting;

public interface IConnector : IConnectorConfigurable
{
    event Func<string, Task>? RawMessageReceived;
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();
    Task<string> SendMessageAsync(string message, string state);
}