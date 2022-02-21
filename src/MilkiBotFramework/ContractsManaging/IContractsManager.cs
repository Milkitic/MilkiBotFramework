using Microsoft.Extensions.DependencyInjection;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.ContractsManaging;

public interface IContractsManager
{
    bool TryGetChannelInfoByMessageContext(MessageIdentity messageIdentity, out ChannelInfo channelInfo, out MemberInfo memberInfo);
    bool TryGetPrivateInfoByMessageContext(MessageIdentity messageIdentity, out PrivateInfo channelInfo);
}