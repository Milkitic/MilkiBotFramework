using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.GoCqHttp.ContactsManaging;

public sealed class GoCqContactsManager : ContactsManagerBase
{
    private readonly GoCqApi _goCqApi;
    private readonly BotOptions _botOptions;
    private readonly ILogger<GoCqContactsManager> _logger;

    public GoCqContactsManager(GoCqApi goCqApi,
        BotOptions botOptions,
        BotTaskScheduler botTaskScheduler,
        ILogger<GoCqContactsManager> logger)
        : base(botTaskScheduler, logger)
    {
        _goCqApi = goCqApi;
        _botOptions = botOptions;
        _logger = logger;
    }

    public override async Task<SelfInfoResult> TryGetSelfInfo()
    {
        var result = await _goCqApi.GetLoginInfo();
        return new SelfInfoResult()
        {
            IsSuccess = true,
            UserId = result.UserId.ToString(),
            Nickname = result.Nickname
        };
    }

    public override async Task<ChannelInfoResult> TryGetChannelInfoByMessageContext(MessageIdentity messageIdentity, string userId)
    {
        if (messageIdentity.Id == null) throw new ArgumentNullException(nameof(messageIdentity.Id));

        var channelId = messageIdentity.Id;
        var subChannelId = messageIdentity.SubId;
        if (!TryGetChannelInfo(channelId, out var channelInfo, subChannelId))
        {
            if (subChannelId == null)
            {
                try
                {
                    var groupInfo = await _goCqApi.GetGroupInfo(long.Parse(channelId));
                    channelInfo = new ChannelInfo(channelId)
                    {
                        Name = string.IsNullOrEmpty(groupInfo.GroupName) ? null : groupInfo.GroupName
                    };
                    AddChannel(channelInfo);
                }
                catch (GoCqApiException ex)
                {
                    _logger.LogWarning("获取群信息时API返回错误：" + ex.Message);
                    return new ChannelInfoResult();
                }
            }
            else
            {
                throw new NotImplementedException(); // guild
            }
        }

        if (!TryGetMemberInfo(channelId, userId, out var memberInfo, subChannelId))
        {
            try
            {
                var groupMember = await _goCqApi.GetGroupMemberDetail(long.Parse(channelId), long.Parse(userId));
                memberInfo = new MemberInfo(groupMember.UserId)
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
                AddMember(channelId, memberInfo);
            }
            catch (GoCqApiException ex)
            {
                _logger.LogWarning("获取群成员信息时API返回错误：" + ex.Message);
                return new ChannelInfoResult { ChannelInfo = channelInfo };
            }
        }

        if (_botOptions.RootAccounts.Contains(userId))
            memberInfo.Authority = MessageAuthority.Root;
        else if (memberInfo.MemberRole != MemberRole.Member)
            memberInfo.Authority = MessageAuthority.Admin;
        else
            memberInfo.Authority = MessageAuthority.Public;

        return new ChannelInfoResult { ChannelInfo = channelInfo, IsSuccess = true, MemberInfo = memberInfo };
    }

    public override async Task<PrivateInfoResult> TryGetPrivateInfoByMessageContext(MessageIdentity messageIdentity)
    {
        if (messageIdentity.Id == null) throw new ArgumentNullException(nameof(messageIdentity.Id));

        var userId = messageIdentity.Id;
        if (!TryGetPrivateInfo(userId, out var privateInfo))
        {
            try
            {
                var stranger = await _goCqApi.GetStrangerInfo(long.Parse(userId));
                privateInfo = new PrivateInfo(userId)
                {
                    Nickname = string.IsNullOrEmpty(stranger.Nickname) ? null : stranger.Nickname
                };
                AddPrivate(privateInfo);
            }
            catch (GoCqApiException ex)
            {
                _logger.LogWarning("获取私聊用户信息时API返回错误：" + ex.Message);
                return new PrivateInfoResult { PrivateInfo = privateInfo };
            }
        }

        privateInfo.Authority = _botOptions.RootAccounts.Contains(userId)
            ? MessageAuthority.Root
            : MessageAuthority.Admin;
        return new PrivateInfoResult { PrivateInfo = privateInfo, IsSuccess = true };
    }
}