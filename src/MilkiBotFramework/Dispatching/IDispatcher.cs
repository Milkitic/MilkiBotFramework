using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Dispatching
{
    public interface IDispatcher
    {
        Task InvokeMessageReceived(InboundMessage inboundMessage);
    }
}
