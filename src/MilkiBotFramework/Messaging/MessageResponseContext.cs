using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Messaging;

public class MessageResponseContext
{
    private readonly MessageContext _messageContext;
    private readonly IMessageApi _messageApi;
    private readonly ILogger<MessageResponseContext> _logger;

    public MessageResponseContext(MessageContext messageContext, IMessageApi messageApi, ILogger<MessageResponseContext> logger)
    {
        _messageContext = messageContext;
        _messageApi = messageApi;
        _logger = logger;
    }

    public async Task QuickReply(string message)
    {
        var identity = _messageContext.Request.Identity;
        if (identity != null &&
            identity != MessageIdentity.MetaMessage &&
            identity != MessageIdentity.NoticeMessage)
        {
            if (identity.MessageType == MessageType.Private)
            {
                await _messageApi.SendPrivateMessageAsync(identity.Id!, message);
            }
            else
            {
                await _messageApi.SendChannelMessageAsync(identity.Id!, message, identity.SubId);
            }
        }
        else
        {
            _logger.LogWarning("Quick reply failed: destination undefined.");
        }
    }
}