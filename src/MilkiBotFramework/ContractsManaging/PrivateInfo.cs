namespace MilkiBotFramework.ContractsManaging;

public sealed class PrivateInfo
{
    public PrivateInfo(string userId)
    {
        UserId = userId;
    }

    public string UserId { get; }
    public string? Remark { get; set; }
    public string? Nickname { get; set; }
    public Avatar? Avatar { get; set; }
}