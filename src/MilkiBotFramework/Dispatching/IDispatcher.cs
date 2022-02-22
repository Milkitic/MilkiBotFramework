using System;
using System.Threading.Tasks;
using MilkiBotFramework.ContractsManaging.Models;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching
{
    public interface IDispatcher
    {
        event Func<MessageRequestContext, ChannelInfo, MemberInfo, Task>? ChannelMessageReceived;
        event Func<MessageRequestContext, PrivateInfo, Task>? PrivateMessageReceived;
        event Func<MessageRequestContext, Task>? SystemMessageReceived;
        event Func<MessageRequestContext, Task>? MetaMessageReceived;
    }
}
