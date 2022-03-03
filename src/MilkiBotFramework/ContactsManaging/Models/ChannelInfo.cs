using System.Collections.Concurrent;

namespace MilkiBotFramework.ContactsManaging.Models;

public sealed class SelfInfo
{
    public string UserId { get; set; }
    public string? Nickname { get; set; }
}

public sealed class ChannelInfo
{
    public ChannelInfo(string channelId)
    {
        ChannelId = channelId;
    }

    public string ChannelId { get; }
    public string? SubChannelId { get; set; }
    public string? Name { get; set; }
    public Avatar? Avatar { get; set; }

    public ConcurrentDictionary<string, MemberInfo> Members { get; } = new();
    public bool IsRootChannel => SubChannelId == null;
}