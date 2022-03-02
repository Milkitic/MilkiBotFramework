using MilkiBotFramework.ContactsManaging.Models;

namespace MilkiBotFramework.ContactsManaging.Results;

public sealed class SelfInfoResult : ResultInfoBase
{
    public SelfInfo? SelfInfo { get; init; }
    public static SelfInfoResult Fail { get; } = new();
}