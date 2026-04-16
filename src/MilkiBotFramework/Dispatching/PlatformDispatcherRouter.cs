using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Dispatching;

public sealed class PlatformDispatcherRouter : IDispatcher
{
    private readonly IPlatformDispatcher[] _dispatchers;
    private readonly ILogger<PlatformDispatcherRouter> _logger;

    public PlatformDispatcherRouter(IEnumerable<IPlatformDispatcher> dispatchers,
        ILogger<PlatformDispatcherRouter> logger)
    {
        _dispatchers = dispatchers.ToArray();
        _logger = logger;
    }

    public async Task InvokeMessageReceived(InboundMessage inboundMessage)
    {
        var dispatcher = _dispatchers.FirstOrDefault(d => d.CanDispatch(inboundMessage));
        if (dispatcher == null)
        {
            _logger.LogWarning("No dispatcher matched inbound message. Transport={Transport}", inboundMessage.Transport);
            return;
        }

        await dispatcher.InvokeMessageReceived(inboundMessage);
    }
}