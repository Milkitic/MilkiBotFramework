namespace MilkiBotFramework.Dispatching;

public class AggregateDispatcher : IDispatcher
{
    private readonly IEnumerable<IDispatcher> _dispatchers;

    public AggregateDispatcher(IEnumerable<IDispatcher> dispatchers)
    {
        _dispatchers = dispatchers;
    }

    public async Task InvokeRawMessageReceived(string rawMessage)
    {
        foreach (var dispatcher in _dispatchers)
        {
            if (dispatcher != this)
                await dispatcher.InvokeRawMessageReceived(rawMessage);
        }
    }
}