using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Connecting;

public class AggregateMessageApi : IMessageApi
{
    private readonly IEnumerable<IMessageApi> _apis;

    public AggregateMessageApi(IEnumerable<IMessageApi> apis)
    {
        _apis = apis;
    }

    public bool Supports(MessageContext messageContext)
    {
        return true;
    }

    public async Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage,
        MessageContext messageContext)
    {
        var api = _apis.FirstOrDefault(k => k != this && k.Supports(messageContext));
        if (api != null)
        {
            return await api.SendPrivateMessageAsync(userId, message, richMessage, messageContext);
        }

        throw new NotSupportedException($"No MessageApi found for context type {messageContext.GetType().Name}");
    }

    public async Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage,
        MessageContext messageContext, string? subChannelId)
    {
        var api = _apis.FirstOrDefault(k => k != this && k.Supports(messageContext));
        if (api != null)
        {
            return await api.SendChannelMessageAsync(channelId, message, richMessage, messageContext, subChannelId);
        }

        throw new NotSupportedException($"No MessageApi found for context type {messageContext.GetType().Name}");
    }
}