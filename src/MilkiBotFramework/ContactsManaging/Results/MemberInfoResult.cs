using MilkiBotFramework.ContactsManaging.Models;

namespace MilkiBotFramework.ContactsManaging.Results;

public sealed class MemberInfoResult : ResultInfoBase
{
    public MemberInfo? MemberInfo { get; init; }

    public static MemberInfoResult Fail { get; } = new();
}