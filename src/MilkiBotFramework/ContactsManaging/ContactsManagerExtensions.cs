using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContactsManaging;

public static class ContactsManagerExtensions
{
    public static async Task<string?> GetIdentityName(this IContactsManager contactsManager, MessageIdentity messageIdentity)
    {
        if (messageIdentity.MessageType == MessageType.Private)
        {
            return (await contactsManager.TryGetOrAddPrivateInfo(messageIdentity.Id!)).PrivateInfo?.Nickname;
        }

        if (messageIdentity.MessageType == MessageType.Channel)
        {
            return (await contactsManager.TryGetOrAddChannelInfo(messageIdentity.Id!, messageIdentity.SubId)).ChannelInfo?.Name;
        }

        return null;
    }
}