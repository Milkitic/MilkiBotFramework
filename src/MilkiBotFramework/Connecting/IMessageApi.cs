using System.Threading.Tasks;

namespace MilkiBotFramework.Connecting;

public interface IMessageApi
{
    Task<string> SendPrivateMessageAsync(string userId, string message);
    Task<string> SendChannelMessageAsync(string channelId, string message, string? subChannelId);
}