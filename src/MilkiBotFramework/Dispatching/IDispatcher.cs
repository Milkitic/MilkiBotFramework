using System;
using System.Threading.Tasks;
using MilkiBotFramework.ContractsManaging.Models;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching
{
    public interface IDispatcher
    {
        event Func<MessageContext, ChannelInfo, MemberInfo, Task>? ChannelMessageReceived;
        event Func<MessageContext, PrivateInfo, Task>? PrivateMessageReceived;
        event Func<MessageContext, Task>? SystemMessageReceived;
        event Func<MessageContext, Task>? MetaMessageReceived;
    }
}
