namespace MilkiBotFramework.ContractsManaging;

public sealed class MemberInfo
{
    public MemberInfo(string userId)
    {
        UserId = userId;
    }

    public string UserId { get; internal set; }
    public string? Card { get; internal set; }
    public string? Nickname { get; internal set; }
    public MemberRole MemberRole { get; internal set; }
}