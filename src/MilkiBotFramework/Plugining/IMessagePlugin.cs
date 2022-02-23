using System.Threading.Tasks;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Plugining;

public interface IMessagePlugin
{
    Task OnMessageReceived(MessageRequestContext request, MessageResponseContext response);
}