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

    public Task<string> SendChannelMessageAsync(string channelId, string content, CancellationToken cancellationToken = default)
    {
        return ResolveSingle().SendChannelMessageAsync(channelId, content, cancellationToken);
    }

    public Task<string> SendChannelMessageAsync(string channelId, IRichMessage message, CancellationToken cancellationToken = default)
    {
        return ResolveSingle().SendChannelMessageAsync(channelId, message, cancellationToken);
    }

    private IPlatformMessageApi Resolve(MessageContext messageContext)
    {
        return _messageApis.FirstOrDefault(api => api.Supports(messageContext))
               ?? throw new InvalidOperationException($"No message api registered for platform '{messageContext.PlatformId ?? "unknown"}'.");
    }

    private IPlatformMessageApi ResolveSingle()
    {
        if (_messageApis.Length == 1)
        {
            return _messageApis[0];
        }

        throw new NotSupportedException(
            "Proactive channel message routing requires a single registered platform or an explicit platform API.");
    }
}