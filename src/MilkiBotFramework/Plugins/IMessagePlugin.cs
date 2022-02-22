using System.Threading.Tasks;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Plugins;

public interface IMessagePlugin
{
    Task OnMessageReceived(MessageRequestContext request, MessageResponseContext response);
}