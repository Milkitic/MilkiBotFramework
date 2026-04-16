using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Connecting;

public sealed class PlatformMessageApiRouter : IMessageApi
{
    private readonly IPlatformMessageApi[] _messageApis;

    public PlatformMessageApiRouter(IEnumerable<IPlatformMessageApi> messageApis)
    {
        _messageApis = messageApis.ToArray();
    }

    public bool Supports(MessageContext messageContext)
    {
        return _messageApis.Any(api => api.Supports(messageContext));
    }

    public Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage,
        MessageContext messageContext)
    {
        return Resolve(messageContext).SendPrivateMessageAsync(userId, message, richMessage, messageContext);
    }

    public Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage,
        MessageContext messageContext, string? subChannelId)
    {
        return Resolve(messageContext).SendChannelMessageAsync(channelId, message, richMessage, messageContext,
            subChannelId);
    }

    private IPlatformMessageApi Resolve(MessageContext messageContext)
    {
        return _messageApis.FirstOrDefault(api => api.Supports(messageContext))
               ?? throw new InvalidOperationException($"No message api registered for platform '{messageContext.PlatformId ?? "unknown"}'.");
    }
}