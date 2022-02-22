using MilkiBotFramework.ContractsManaging.Models;

namespace MilkiBotFramework.ContractsManaging.Results;

public sealed class ChannelInfoResult : ResultInfoBase
{
    public ChannelInfo? ChannelInfo { get; init; }
    public MemberInfo? MemberInfo { get; init; }
}