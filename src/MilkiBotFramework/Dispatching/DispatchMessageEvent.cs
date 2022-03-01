using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

public sealed class DispatchMessageEvent : IEventBusEvent
{
    public DispatchMessageEvent(MessageContext messageContext, MessageType messageType)
    {
        MessageContext = messageContext;
        MessageType = messageType;
    }

    public MessageContext MessageContext { get; }
    public MessageType MessageType { get; }
}