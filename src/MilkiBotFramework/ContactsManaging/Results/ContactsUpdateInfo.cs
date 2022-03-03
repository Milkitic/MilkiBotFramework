namespace MilkiBotFramework.ContactsManaging.Results;

public sealed class ContactsUpdateInfo 
{
    public ContactsUpdateInfo(string? id)
    {
        Id = id;
    }

    public string? Id { get; }
    public string? SubId { get; init; }
    public string? Card { get; init; }
    public string? NickName { get; init; }
    public ContactsUpdateRole ContactsUpdateRole { get; init; }
    public ContactsUpdateType ContactsUpdateType { get; init; }
}