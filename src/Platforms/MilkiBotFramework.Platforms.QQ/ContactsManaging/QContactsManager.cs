using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.QQ.Connecting;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.QQ.ContactsManaging;

public sealed class QContactsManager : ContactsManagerBase
{
    private readonly QApi _goCqApi;
    private readonly ILogger<QContactsManager> _logger;

    public QContactsManager(QApi goCqApi,
        BotTaskScheduler botTaskScheduler,
        ILogger<QContactsManager> logger,
        EventBus eventBus)
        : base(botTaskScheduler, logger, eventBus)
    {
        _goCqApi = goCqApi;
        _logger = logger;
    }

    public override async Task<SelfInfoResult> TryGetOrUpdateSelfInfo()
    {
        var baseResult = await base.TryGetOrUpdateSelfInfo();
        if (baseResult.IsSuccess) return baseResult;

        throw new NotImplementedException();
    }

    public override async Task<ChannelInfoResult> TryGetOrAddChannelInfo(string channelId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddChannelInfo(channelId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;

        throw new NotImplementedException();
    }

    public override async Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddMemberInfo(channelId, userId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;

        throw new NotImplementedException();
    }

    public override async Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        var baseResult = await base.TryGetOrAddPrivateInfo(userId);
        if (baseResult.IsSuccess) return baseResult;

        throw new NotImplementedException();
    }

    protected override bool GetContactsUpdateInfo(MessageContext messageContext, [NotNullWhen(true)] out ContactsUpdateInfo? updateInfo)
    {
        // todo
        updateInfo = null;
        return false;
    }

    protected override void GetContactsCore(
        out Dictionary<string, ChannelInfo> channels,
        out Dictionary<string, ChannelInfo> subChannels,
        out Dictionary<string, PrivateInfo> privates)
    {
        throw new NotImplementedException();
    }
}