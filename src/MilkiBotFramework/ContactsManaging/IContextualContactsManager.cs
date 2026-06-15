using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContactsManaging;

public interface IContextualContactsManager
{
    Task<SelfInfoResult> TryGetOrUpdateSelfInfo(MessageContext messageContext);
    Task<MemberInfoResult> TryGetOrAddMemberInfo(MessageContext messageContext, string channelId, string userId,
        string? subChannelId = null);
    Task<ChannelInfoResult> TryGetOrAddChannelInfo(MessageContext messageContext, string channelId,
        string? subChannelId = null);
    Task<PrivateInfoResult> TryGetOrAddPrivateInfo(MessageContext messageContext, string userId);
}
