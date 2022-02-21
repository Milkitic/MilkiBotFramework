namespace MilkiBotFramework.ContractsManaging;

public sealed class PrivateInfo
{
    public PrivateInfo(string userId)
    {
        UserId = userId;
    }

    public string UserId { get; internal set; }
    public string? Remark { get; internal set; }
    public string? Nickname { get; internal set; }
    public Avatar? Avatar { get; internal set; }
}