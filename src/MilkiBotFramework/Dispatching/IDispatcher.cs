using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching
{
    public interface IDispatcher
    {
        event Func<MessageContext, Task>? PublicMessageReceived;
        event Func<MessageContext, Task>? PrivateMessageReceived;
        event Func<MessageContext, Task>? SystemMessageReceived;
        event Func<MessageContext, Task>? MetaMessageReceived;
    }
}
