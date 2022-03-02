using MilkiBotFramework.ContactsManaging.Models;

namespace MilkiBotFramework.ContactsManaging.Results;

public sealed class PrivateInfoResult : ResultInfoBase
{
    public PrivateInfo? PrivateInfo { get; init; }
    public static PrivateInfoResult Fail { get; } = new();
}