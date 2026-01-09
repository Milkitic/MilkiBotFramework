using System.Text;

namespace MilkiBotFramework.Connecting;

public class AggregateConnector : IConnector
{
    private readonly IEnumerable<IConnector> _connectors;
    public event Func<string, Task>? RawMessageReceived;

    public AggregateConnector(IEnumerable<IConnector> connectors)
    {
        _connectors = connectors;
        foreach (var connector in _connectors)
        {
            connector.RawMessageReceived += async msg =>
            {
                if (RawMessageReceived != null)
                {
                    await RawMessageReceived(msg);
                }
            };
        }
    }

    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public TimeSpan ErrorReconnectTimeout { get; set; }
    public TimeSpan MessageTimeout { get; set; }
    public Encoding? Encoding { get; set; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var tasks = _connectors.Select(c => c.ConnectAsync(cancellationToken));
        await Task.WhenAll(tasks);
    }

    public async Task DisconnectAsync()
    {
        var tasks = _connectors.Select(c => c.DisconnectAsync());
        await Task.WhenAll(tasks);
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        var sb = new StringBuilder();
        foreach (var connector in _connectors)
        {
            try
            {
                var result = await connector.SendMessageAsync(message, state);
                sb.AppendLine($"[{connector.GetType().Name}]: {result}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[{connector.GetType().Name} Error]: {ex.Message}");
            }
        }

        return sb.ToString();
    }
}