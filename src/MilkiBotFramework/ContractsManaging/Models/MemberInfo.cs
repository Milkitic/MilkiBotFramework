using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContractsManaging.Models;

public sealed class MemberInfo
{
    public MemberInfo(string userId)
    {
        UserId = userId;
    }

    public string UserId { get; }
    public string? Card { get; set; }
    public string? Nickname { get; set; }
    public MemberRole MemberRole { get; set; }
    public MessageAuthority Authority { get; set; }
}