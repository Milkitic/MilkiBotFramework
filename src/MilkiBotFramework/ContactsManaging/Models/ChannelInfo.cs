using System.Collections.Concurrent;

namespace MilkiBotFramework.ContactsManaging.Models;

public sealed class ChannelInfo
{
    public ChannelInfo(string channelId, IEnumerable<MemberInfo>? members = null)
    {
        ChannelId = channelId;
        if (members != null)
            Members = new ConcurrentDictionary<string, MemberInfo>(
                members.ToDictionary(k => k.UserId, k => k)
            );
    }

    public string ChannelId { get; }
    public string? SubChannelId { get; set; }
    public string? Name { get; set; }
    public Avatar? Avatar { get; set; }

    public ConcurrentDictionary<string, MemberInfo> Members { get; } = new();
    public bool IsRootChannel => SubChannelId == null;
}