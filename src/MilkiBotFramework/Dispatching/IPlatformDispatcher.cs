using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Dispatching;

public interface IPlatformDispatcher : IDispatcher
{
    string PlatformId { get; }
    bool CanDispatch(InboundMessage inboundMessage);
}