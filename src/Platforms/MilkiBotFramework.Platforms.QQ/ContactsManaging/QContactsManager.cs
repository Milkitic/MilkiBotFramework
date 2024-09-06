using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.QQ.ContactsManaging;

public sealed class QContactsManager : ContactsManagerBase
{
    private readonly ILogger<QContactsManager> _logger;

    public QContactsManager(
        BotTaskScheduler botTaskScheduler,
        ILogger<QContactsManager> logger,
        EventBus eventBus)
        : base(botTaskScheduler, logger, eventBus)
    {
        _logger = logger;
    }

    public void UpdateSelfInfo(SelfInfo selfInfo)
    {
        SelfInfo = selfInfo;
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

        var channelInfo = new ChannelInfo(channelId)
        {
            Name = channelId
        };
        ChannelMapping.AddOrUpdate(channelInfo.ChannelId, channelInfo, (_, _) => channelInfo);
        return new ChannelInfoResult { IsSuccess = true, ChannelInfo = channelInfo };
    }

    public override async Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddMemberInfo(channelId, userId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;


        var success = ChannelMapping.TryGetValue(channelId, out var channelInfo);
        if (!success)
        {
            var channelResult = await TryGetOrAddChannelInfo(channelId, subChannelId);
            success = channelResult.IsSuccess;
            channelInfo = channelResult.ChannelInfo;
        }

        var memberInfo = new MemberInfo(channelId, userId, subChannelId)
        {
            Nickname = userId,
            Card = userId,
            MemberRole = MemberRole.Member
        };

        if (success && channelInfo != null)
        {
            channelInfo.Members.AddOrUpdate(userId, memberInfo, (_, _) => memberInfo);
            return new MemberInfoResult { IsSuccess = true, MemberInfo = memberInfo };
        }

        return MemberInfoResult.Fail;
    }

    public override async Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        var baseResult = await base.TryGetOrAddPrivateInfo(userId);
        if (baseResult.IsSuccess) return baseResult;

        var privateInfo = new PrivateInfo(userId)
        {
            Nickname = userId,
            Remark = userId
        };
        PrivateMapping.AddOrUpdate(privateInfo.UserId, privateInfo, (_, _) => privateInfo);
        return new PrivateInfoResult { IsSuccess = true, PrivateInfo = privateInfo };
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
        channels = new Dictionary<string, ChannelInfo>();
        subChannels = new Dictionary<string, ChannelInfo>();
        privates = new Dictionary<string, PrivateInfo>();
    }
}