namespace MilkiBotFramework.ContactsManaging;

public sealed class ContactsUpdateEvent
{
    public IReadOnlyList<ContactsUpdateSingleEvent> Events { get; init; } = Array.Empty<ContactsUpdateSingleEvent>();

    public static explicit operator ContactsUpdateEvent(ContactsUpdateSingleEvent single)
    {
        return new ContactsUpdateEvent
        {
            Events = new[] { single }
        };
    }
}