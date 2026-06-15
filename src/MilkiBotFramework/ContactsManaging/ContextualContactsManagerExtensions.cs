using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContactsManaging;

internal static class ContextualContactsManagerExtensions
{
    public static Task<SelfInfoResult> TryGetOrUpdateSelfInfoSafe(this IContactsManager contactsManager,
        MessageContext messageContext)
    {
        return contactsManager is IContextualContactsManager contextualContactsManager
            ? contextualContactsManager.TryGetOrUpdateSelfInfo(messageContext)
            : contactsManager.TryGetOrUpdateSelfInfo();
    }

    public static Task<MemberInfoResult> TryGetOrAddMemberInfoSafe(this IContactsManager contactsManager,
        MessageContext messageContext,
        string channelId,
        string userId,
        string? subChannelId = null)
    {
        return contactsManager is IContextualContactsManager contextualContactsManager
            ? contextualContactsManager.TryGetOrAddMemberInfo(messageContext, channelId, userId, subChannelId)
            : contactsManager.TryGetOrAddMemberInfo(channelId, userId, subChannelId);
    }

    public static Task<ChannelInfoResult> TryGetOrAddChannelInfoSafe(this IContactsManager contactsManager,
        MessageContext messageContext,
        string channelId,
        string? subChannelId = null)
    {
        return contactsManager is IContextualContactsManager contextualContactsManager
            ? contextualContactsManager.TryGetOrAddChannelInfo(messageContext, channelId, subChannelId)
            : contactsManager.TryGetOrAddChannelInfo(channelId, subChannelId);
    }

    public static Task<PrivateInfoResult> TryGetOrAddPrivateInfoSafe(this IContactsManager contactsManager,
        MessageContext messageContext,
        string userId)
    {
        return contactsManager is IContextualContactsManager contextualContactsManager
            ? contextualContactsManager.TryGetOrAddPrivateInfo(messageContext, userId)
            : contactsManager.TryGetOrAddPrivateInfo(userId);
    }
}
