using System.Collections.Generic;

namespace MilkiBotFramework.ContractsManaging;

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

    public Dictionary<string, MemberInfo> Members { get; } = new();
    public bool IsRootChannel => SubChannelId == null;
}