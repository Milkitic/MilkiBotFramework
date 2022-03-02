using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Event;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.GoCqHttp.ContactsManaging;

public sealed class GoCqContactsManager : ContactsManagerBase
{
    private readonly GoCqApi _goCqApi;
    private readonly ILogger<GoCqContactsManager> _logger;

    public GoCqContactsManager(GoCqApi goCqApi,
        BotTaskScheduler botTaskScheduler,
        ILogger<GoCqContactsManager> logger,
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

        var result = await _goCqApi.GetLoginInfo();
        var selfInfo = new SelfInfo
        {
            Nickname = result.Nickname,
            UserId = result.UserId.ToString()
        };
        SelfInfo = selfInfo;
        return new SelfInfoResult
        {
            IsSuccess = true,
            SelfInfo = selfInfo
        };
    }

    public override async Task<ChannelInfoResult> TryGetOrAddChannelInfo(string channelId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddChannelInfo(channelId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;
        if (subChannelId == null)
        {
            try
            {
                var groupInfo = await _goCqApi.GetGroupInfo(long.Parse(channelId));
                var channelInfo = new ChannelInfo(channelId)
                {
                    Name = string.IsNullOrEmpty(groupInfo.GroupName) ? null : groupInfo.GroupName
                };
                ChannelMapping.AddOrUpdate(channelInfo.ChannelId, channelInfo, (_, _) => channelInfo);
                return new ChannelInfoResult { IsSuccess = true, ChannelInfo = channelInfo };
            }
            catch (GoCqApiException ex)
            {
                _logger.LogWarning("获取群信息时API返回错误：" + ex.Message);
                return ChannelInfoResult.Fail;
            }
        }
        else
        {
            // todo: guild
            return ChannelInfoResult.Fail;
        }
    }

    public override async Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddMemberInfo(channelId, userId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;
        GroupMember groupMember;
        try
        {
            groupMember = await _goCqApi.GetGroupMemberDetail(long.Parse(channelId), long.Parse(userId));
        }
        catch (GoCqApiException ex)
        {
            _logger.LogWarning("获取群成员信息时API返回错误：" + ex.Message);
            return MemberInfoResult.Fail;
        }

        var memberInfo = new MemberInfo(groupMember.UserId)
        {
            Nickname = string.IsNullOrEmpty(groupMember.Nickname) ? null : groupMember.Nickname,
            Card = string.IsNullOrEmpty(groupMember.Card) ? null : groupMember.Card,
            MemberRole = groupMember.Role switch
            {
                "owner" => MemberRole.Owner,
                "admin" => MemberRole.Admin,
                "member" => MemberRole.Member,
                _ => MemberRole.Member
            }
        };

        var success = ChannelMapping.TryGetValue(channelId, out var channelInfo);
        if (!success)
        {
            var channelResult = await TryGetOrAddChannelInfo(channelId, subChannelId);
            success = channelResult.IsSuccess;
            channelInfo = channelResult.ChannelInfo;
        }

        if (success && channelInfo != null)
        {
            channelInfo.Members.AddOrUpdate(memberInfo.UserId, memberInfo, (_, _) => memberInfo);
            return new MemberInfoResult { IsSuccess = true, MemberInfo = memberInfo };
        }

        return MemberInfoResult.Fail;
    }

    public override async Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        var baseResult = await base.TryGetOrAddPrivateInfo(userId);
        if (baseResult.IsSuccess) return baseResult;
        try
        {
            var stranger = await _goCqApi.GetStrangerInfo(long.Parse(userId));
            var privateInfo = new PrivateInfo(userId)
            {
                Nickname = string.IsNullOrEmpty(stranger.Nickname) ? null : stranger.Nickname
            };
            PrivateMapping.AddOrUpdate(privateInfo.UserId, privateInfo, (_, _) => privateInfo);
            return new PrivateInfoResult { IsSuccess = true, PrivateInfo = privateInfo };
        }
        catch (GoCqApiException ex)
        {
            _logger.LogWarning("获取私聊用户信息时API返回错误：" + ex.Message);
            return PrivateInfoResult.Fail;
        }
    }
}