using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using MilkiBotFramework.ContractsManaging.Results;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContractsManaging;

public interface IContractsManager
{
    Task<ChannelInfoResult> TryGetChannelInfoByMessageContext(MessageIdentity messageIdentity, string userId);
    Task<PrivateInfoResult> TryGetPrivateInfoByMessageContext(MessageIdentity messageIdentity);
}