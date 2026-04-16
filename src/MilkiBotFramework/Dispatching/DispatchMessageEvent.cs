using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

public sealed class DispatchMessageEvent : IEventBusEvent
{
    public DispatchMessageEvent(MessageContext messageContext)
    {
        MessageContext = messageContext;
        MessageType = messageContext.MessageIdentity?.MessageType
                      ?? throw new ArgumentException("Message identity is required.", nameof(messageContext));
    }

    public MessageContext MessageContext { get; }
    public MessageType MessageType { get; }
}