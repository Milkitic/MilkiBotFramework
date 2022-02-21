using System;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContractsManaging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.GoCqHttp.ContractsManaging;

public sealed class GoCqContractsManager : ContractsManagerBase
{
    public GoCqContractsManager(BotTaskScheduler botTaskScheduler, ILogger<GoCqContractsManager> logger)
        : base(botTaskScheduler, logger)
    {
    }

    public override bool TryGetChannelInfoByMessageContext(MessageIdentity messageIdentity,
        out ChannelInfo channelInfo,
        out MemberInfo memberInfo)
    {
        throw new NotImplementedException();
    }

    public override bool TryGetPrivateInfoByMessageContext(MessageIdentity messageIdentity,
        out PrivateInfo channelInfo)
    {
        throw new NotImplementedException();
    }
}