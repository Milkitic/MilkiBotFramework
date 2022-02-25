using System;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching
{
    public interface IDispatcher
    {
        event Func<MessageContext, Task>? ChannelMessageReceived;
        event Func<MessageContext, Task>? PrivateMessageReceived;
        event Func<MessageContext, Task>? SystemMessageReceived;
        event Func<MessageContext, Task>? MetaMessageReceived;
    }
}
