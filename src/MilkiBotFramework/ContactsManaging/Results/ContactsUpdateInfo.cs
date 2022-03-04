using MilkiBotFramework.ContactsManaging.Models;

namespace MilkiBotFramework.ContactsManaging.Results;

public sealed class ContactsUpdateInfo
{
    public ContactsUpdateInfo(string id)
    {
        Id = id;
    }

    public string Id { get; }
    public string? SubId { get; init; }
    public string? UserId { get; init; }
    public string? Remark { get; init; }
    public string? Name { get; init; }
    public ContactsUpdateRole ContactsUpdateRole { get; init; }
    public ContactsUpdateType ContactsUpdateType { get; init; }
    public MemberRole? MemberRole { get; set; }
    public IEnumerable<MemberInfo>? Members { get; set; }
}