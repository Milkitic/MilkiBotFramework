using System.Collections.Generic;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Plugining;

public interface IMessagePlugin
{
    IAsyncEnumerable<IResponse> OnMessageReceived(MessageContext context);
}