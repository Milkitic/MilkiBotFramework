using System;
using MilkiBotFramework.ContractsManaging.Models;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Messaging;

public class MessageRequestContext
{
    private readonly IRichMessageConverter _richMessageConverter;
    public string RawTextMessage { get; }

    public string? MessageId { get; set; }
    public string? UserId { get; set; }
    public string? SelfId { get; set; }
    public virtual string? TextMessage { get; set; }

    public CommandLineResult? CommandLineResult { get; set; }

    public MemberInfo? MemberInfo { get; set; }
    public ChannelInfo? ChannelInfo { get; set; }
    public PrivateInfo? PrivateInfo { get; set; }

    public MessageIdentity? Identity { get; set; }
    public MessageAuthority Authority { get; set; }
    public MessageAuthority MinimumAuthority { get; set; }

    public DateTimeOffset ReceivedTime { get; set; }

    public MessageRequestContext(string rawTextMessage, IRichMessageConverter richMessageConverter)
    {
        _richMessageConverter = richMessageConverter;
        RawTextMessage = rawTextMessage;
    }

    public bool ValidateAuthority(MessageAuthority authority)
    {
        return authority >= MinimumAuthority;
    }

    public RichMessage GetRichMessage()
    {
        return _richMessageConverter.Decode(TextMessage.AsMemory());
    }
}