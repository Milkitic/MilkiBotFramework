using MilkiBotFramework.Event;

namespace MilkiBotFramework.ContactsManaging;

public sealed class ContactsUpdateEvent : IEventBusEvent
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