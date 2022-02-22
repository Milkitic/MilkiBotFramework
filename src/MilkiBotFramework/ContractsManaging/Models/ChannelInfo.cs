using System.Collections.Concurrent;

namespace MilkiBotFramework.ContractsManaging.Models;

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