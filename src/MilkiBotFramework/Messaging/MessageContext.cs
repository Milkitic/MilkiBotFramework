using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Messaging;

/// <summary>
/// 表示一个类，用以传递单条消息的上下文信息。
/// </summary>
public class MessageContext
{
    private readonly IRichMessageConverter _richMessageConverter;

    public MessageContext(IRichMessageConverter richMessageConverter)
    {
        _richMessageConverter = richMessageConverter;
    }

    public InboundMessage InboundMessage { get; internal set; } = null!;
    public string? RawTextMessage => InboundMessage.RawText;
    public string? PlatformId { get; set; }

    public string? MessageId { get; set; }
    public virtual string? TextMessage { get; set; }

    public MemberInfo? MemberInfo { get; set; }
    public ChannelInfo? ChannelInfo { get; set; }
    public PrivateInfo? PrivateInfo { get; set; }

    public MessageUserIdentity? MessageUserIdentity { get; set; }
    public MessageIdentity? MessageIdentity { get; set; }
    public MessageAuthority Authority { get; set; }
    public DateTimeOffset ReceivedTime { get; set; }

    public CommandLineResult? CommandLineResult { get; set; }

    public RichMessage GetRichMessage()
    {
        if (_richMessageConverter is IPlatformRichMessageConverterRouter router)
        {
            return router.Decode(this, (TextMessage ?? string.Empty).AsMemory());
        }

        return _richMessageConverter.Decode((TextMessage ?? string.Empty).AsMemory());
    }
}