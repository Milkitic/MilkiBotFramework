using MilkiBotFramework.ContactsManaging.Models;

namespace MilkiBotFramework.ContactsManaging.Results;

public sealed class ChannelInfoResult : ResultInfoBase
{
    public ChannelInfo? ChannelInfo { get; init; }
    public static ChannelInfoResult Fail { get; } = new();
}