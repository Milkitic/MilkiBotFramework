using System.Collections.Generic;

namespace MilkiBotFramework.ContractsManaging;

public sealed class ChannelInfo
{
    public ChannelInfo(string channelId)
    {
        ChannelId = channelId;
    }

    public string ChannelId { get; internal set; }
    public string? SubChannelId { get; internal set; }
    public string? Name { get; internal set; }
    public Dictionary<string, MemberInfo> Members { get; } = new();
    public Avatar? Avatar { get; internal set; }

    public bool IsRootChannel => SubChannelId == null;
}