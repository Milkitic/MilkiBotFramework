using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContactsManaging;

public interface IPlatformContactsManagerRouter
{
    IPlatformContactsManager ResolveRequired(MessageContext messageContext);
}