using System.Collections.Generic;
using System.Threading.Tasks;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContactsManaging;

public interface IContactsManager
{
    void Initialize();
    Task<SelfInfoResult> TryGetSelfInfo();
    Task<ChannelInfoResult> TryGetChannelInfoByMessageContext(MessageIdentity messageIdentity, string userId);
    Task<PrivateInfoResult> TryGetPrivateInfoByMessageContext(MessageIdentity messageIdentity);
    IEnumerable<ChannelInfo> GetAllMainChannels();
    IEnumerable<PrivateInfo> GetAllPrivates();
}