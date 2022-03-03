using System.Collections.Generic;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Plugining;

public interface IMessagePlugin
{
    IAsyncEnumerable<IResponse> OnMessageReceived(MessageContext context);
    Task<IResponse?> OnBindingFailed(BindingException bindingException, MessageContext context);
}