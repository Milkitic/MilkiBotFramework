using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Loading;

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

    public string RawTextMessage { get; internal set; } = null!;

    public string? MessageId { get; set; }
    public virtual string? TextMessage { get; set; }

    public MemberInfo? MemberInfo { get; set; }
    public ChannelInfo? ChannelInfo { get; set; }
    public PrivateInfo? PrivateInfo { get; set; }

    public MessageUserIdentity? MessageUserIdentity { get; set; }
    public MessageIdentity? MessageIdentity { get; set; }
    public MessageAuthority Authority { get; set; }
    public DateTimeOffset ReceivedTime { get; set; }

    public IReadOnlyList<PluginInfo> ExecutedPlugins { get; } = new List<PluginInfo>();
    public List<PluginInfo> NextPlugins { get; internal set; } = new();
    public CommandLineResult? CommandLineResult { get; internal set; }

    public RichMessage GetRichMessage()
    {
        return _richMessageConverter.Decode(TextMessage.AsMemory());
    }
}