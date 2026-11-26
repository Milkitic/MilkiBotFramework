using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Connecting;

public interface IMessageApi
{
    bool Supports(MessageContext messageContext);
    Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage, MessageContext messageContext);
    Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, MessageContext messageContext, string? subChannelId);

    Task<string> SendChannelMessageAsync(string channelId, string content, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"Proactive channel text message is not supported by {GetType().Name}.");

    Task<string> SendChannelMessageAsync(string channelId, IRichMessage message, CancellationToken cancellationToken = default)
        => throw new NotSupportedException($"Proactive channel rich message is not supported by {GetType().Name}.");
}