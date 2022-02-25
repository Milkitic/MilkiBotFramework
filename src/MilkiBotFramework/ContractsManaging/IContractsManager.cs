using System.Collections.Generic;
using System.Threading.Tasks;
using MilkiBotFramework.ContractsManaging.Models;
using MilkiBotFramework.ContractsManaging.Results;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContractsManaging;

public interface IContractsManager
{
    void Initialize();
    Task<ChannelInfoResult> TryGetChannelInfoByMessageContext(MessageIdentity messageIdentity, string userId);
    Task<PrivateInfoResult> TryGetPrivateInfoByMessageContext(MessageIdentity messageIdentity);
    IEnumerable<ChannelInfo> GetAllMainChannels();
    IEnumerable<PrivateInfo> GetAllPrivates();
}