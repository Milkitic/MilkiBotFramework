namespace MilkiBotFramework.ContactsManaging.Models;

public sealed class MemberInfo
{
    public MemberInfo(string channelId, string userId, string? subChannelId)
    {
        ChannelId = channelId;
        UserId = userId;
        SubChannelId = subChannelId;
    }

    public string ChannelId { get; }
    public string UserId { get; }
    public string? SubChannelId { get; }
    public string? Card { get; set; }
    public string? Nickname { get; set; }
    public MemberRole MemberRole { get; set; }
}