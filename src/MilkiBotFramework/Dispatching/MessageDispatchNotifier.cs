using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

public sealed class MessageDispatchNotifier
{
    public event Func<MessageContext, Task>? MessageDispatched;

    public Task NotifyAsync(MessageContext messageContext)
    {
        var handlers = MessageDispatched;
        if (handlers == null)
            return Task.CompletedTask;

        return Task.WhenAll(handlers
            .GetInvocationList()
            .Cast<Func<MessageContext, Task>>()
            .Select(handler => handler(messageContext)));
    }
}