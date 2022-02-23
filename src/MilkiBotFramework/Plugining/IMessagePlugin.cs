using System.Threading.Tasks;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Plugining;

public interface IMessagePlugin
{
    Task OnMessageReceived(MessageContext context);
}