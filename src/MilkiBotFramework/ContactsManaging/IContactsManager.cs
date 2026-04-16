using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;

namespace MilkiBotFramework.ContactsManaging;

public interface IContactsManager
{
    event Func<ContactsUpdateEvent, Task>? ContactsUpdated;

    void InitializeTasks();
    Task HandleMessageAsync(Messaging.MessageContext messageContext);
    Task<SelfInfoResult> TryGetOrUpdateSelfInfo();
    Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null);
    Task<ChannelInfoResult> TryGetOrAddChannelInfo(string channelId, string? subChannelId = null);
    Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId);
    IEnumerable<ChannelInfo> GetAllChannels();
    IEnumerable<MemberInfo> GetAllMembers(string channelId, string? subChannelId = null);
    IEnumerable<PrivateInfo> GetAllPrivates();
}