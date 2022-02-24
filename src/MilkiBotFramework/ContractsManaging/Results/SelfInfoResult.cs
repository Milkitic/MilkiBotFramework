namespace MilkiBotFramework.ContractsManaging.Results;

public sealed class SelfInfoResult : ResultInfoBase
{
    public string UserId { get; init; }
    public string? Nickname { get; init; }
}