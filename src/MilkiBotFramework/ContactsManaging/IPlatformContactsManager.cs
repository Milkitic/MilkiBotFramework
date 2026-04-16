using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContactsManaging;

public interface IPlatformContactsManager : IContactsManager
{
    string PlatformId { get; }
    bool Supports(MessageContext messageContext);
}