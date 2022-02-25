using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Messaging;

public class MessageContext
{
    private readonly IRichMessageConverter _richMessageConverter;
    private readonly IMessageApi _messageApi;
    private readonly ILogger<MessageContext> _logger;

    public MessageContext(IRichMessageConverter richMessageConverter,
        IMessageApi messageApi,
        ILogger<MessageContext> logger)
    {
        _richMessageConverter = richMessageConverter;
        _messageApi = messageApi;
        _logger = logger;
    }

    public string RawTextMessage { get; internal set; } = null!;

    public string? MessageId { get; set; }
    public string? UserId { get; set; }
    public string? SelfId { get; set; }
    public virtual string? TextMessage { get; set; }

    public MemberInfo? MemberInfo { get; set; }
    public ChannelInfo? ChannelInfo { get; set; }
    public PrivateInfo? PrivateInfo { get; set; }

    public MessageIdentity? MessageIdentity { get; set; }
    public MessageAuthority Authority =>
        MessageIdentity?.MessageType switch
        {
            MessageType.Private => PrivateInfo!.Authority,
            MessageType.Channel => MemberInfo!.Authority,
            _ => MessageAuthority.Unspecified
        };

    public DateTimeOffset ReceivedTime { get; set; }

    public bool ValidateAuthority(MessageAuthority requiredAuthority)
    {
        return Authority >= requiredAuthority;
    }

    public RichMessage GetRichMessage()
    {
        return _richMessageConverter.Decode(TextMessage.AsMemory());
    }
}