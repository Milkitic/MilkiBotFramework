using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Connecting;

public interface IMessageApi
{
    Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage);
    Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, string? subChannelId);
}