using System;
using MilkiBotFramework.ContractsManaging.Models;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Messaging;

public class MessageResponseContext
{

}
public class MessageRequestContext
{
    public string RawTextMessage { get; }

    public string? MessageId { get; set; }
    public string? UserId { get; set; }
    public string? SelfId { get; set; }

    public CommandLineResult? CommandLineResult { get; set; }

    public MemberInfo? MemberInfo { get; set; }
    public ChannelInfo? ChannelInfo { get; set; }
    public PrivateInfo? PrivateInfo { get; set; }

    public MessageIdentity? Identity { get; set; }
    public MessageAuthority Authority { get; set; }
    public MessageAuthority MinimumAuthority { get; set; }

    public DateTimeOffset ReceivedTime { get; set; }

    public MessageRequestContext(string rawTextMessage)
    {
        RawTextMessage = rawTextMessage;
    }

    public bool ValidateAuthority(MessageAuthority authority)
    {
        return authority >= MinimumAuthority;
    }
}