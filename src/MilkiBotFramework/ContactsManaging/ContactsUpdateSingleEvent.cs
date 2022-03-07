using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;

namespace MilkiBotFramework.ContactsManaging;

public sealed class ContactsUpdateSingleEvent
{
    public string? ChangedPath { get; init; }
    public MemberInfo? MemberInfo { get; init; }
    public PrivateInfo? PrivateInfo { get; init; }
    public ChannelInfo? ChannelInfo { get; init; }
    public ChannelInfo? SubChannelInfo { get; init; }
    public ContactsUpdateRole UpdateRole { get; init; }
    public ContactsUpdateType UpdateType { get; init; }
}