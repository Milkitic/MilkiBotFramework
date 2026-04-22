using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.OneBot.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.OneBot.ContactsManaging;

public sealed class OneBotContactsManager : ContactsManagerBase
{
    public override string PlatformId => PlatformIds.OneBot;

    private readonly OneBotApi _oneBotApi;
    private readonly ILogger<OneBotContactsManager> _logger;
    private readonly ConcurrentDictionary<string, AccountContactsState> _accounts =
        new(StringComparer.OrdinalIgnoreCase);

    public OneBotContactsManager(OneBotApi oneBotApi,
        BotTaskScheduler botTaskScheduler,
        ILogger<OneBotContactsManager> logger)
        : base(botTaskScheduler, logger)
    {
        _oneBotApi = oneBotApi;
        _logger = logger;
    }

    protected override void InitializeTasksCore()
    {
        // OneBot multi-account mode maintains contacts per self_id, so the base single-cache refresh task is disabled.
    }

    public override Task<SelfInfoResult> TryGetOrUpdateSelfInfo()
    {
        throw new NotSupportedException("OneBot multi-account mode requires MessageContext/self_id.");
    }

    public override Task<SelfInfoResult> TryGetOrUpdateSelfInfo(MessageContext messageContext)
    {
        return TryGetOrUpdateSelfInfoCore(GetAccountState(GetAccountKey(messageContext)), messageContext.SelfId);
    }

    public override Task<ChannelInfoResult> TryGetOrAddChannelInfo(string channelId, string? subChannelId = null)
    {
        throw new NotSupportedException("OneBot multi-account mode requires MessageContext/self_id.");
    }

    public override Task<ChannelInfoResult> TryGetOrAddChannelInfo(MessageContext messageContext, string channelId,
        string? subChannelId = null)
    {
        return TryGetOrAddChannelInfoCore(GetAccountState(GetAccountKey(messageContext)), channelId, subChannelId, messageContext.SelfId);
    }

    public override Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null)
    {
        throw new NotSupportedException("OneBot multi-account mode requires MessageContext/self_id.");
    }

    public override Task<MemberInfoResult> TryGetOrAddMemberInfo(MessageContext messageContext, string channelId,
        string userId, string? subChannelId = null)
    {
        return TryGetOrAddMemberInfoCore(GetAccountState(GetAccountKey(messageContext)), channelId, userId, subChannelId, messageContext.SelfId);
    }

    public override Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        throw new NotSupportedException("OneBot multi-account mode requires MessageContext/self_id.");
    }

    public override Task<PrivateInfoResult> TryGetOrAddPrivateInfo(MessageContext messageContext, string userId)
    {
        return TryGetOrAddPrivateInfoCore(GetAccountState(GetAccountKey(messageContext)), userId, messageContext.SelfId);
    }

    public override IEnumerable<ChannelInfo> GetAllChannels()
    {
        return _accounts.Values.SelectMany(account => account.ChannelMapping.Values);
    }

    public override IEnumerable<MemberInfo> GetAllMembers(string channelId, string? subChannelId = null)
    {
        foreach (var account in _accounts.Values)
        {
            if (TryGetChannelOrSubChannel(account, channelId, subChannelId, out var channelInfo))
            {
                return channelInfo.Members.Values;
            }
        }

        return Array.Empty<MemberInfo>();
    }

    public override IEnumerable<PrivateInfo> GetAllPrivates()
    {
        return _accounts.Values.SelectMany(account => account.PrivateMapping.Values);
    }

    protected override bool GetContactsUpdateInfo(MessageContext messageContext, [NotNullWhen(true)] out ContactsUpdateInfo? updateInfo)
    {
        // todo
        updateInfo = null;
        return false;
    }

    protected override bool GetContactsCore(
        out Dictionary<string, ChannelInfo> channels,
        out Dictionary<string, ChannelInfo> subChannels,
        out Dictionary<string, PrivateInfo> privates)
    {
        channels = new Dictionary<string, ChannelInfo>();
        subChannels = new Dictionary<string, ChannelInfo>();
        privates = new Dictionary<string, PrivateInfo>();
        return false;
    }

    private async Task<SelfInfoResult> TryGetOrUpdateSelfInfoCore(AccountContactsState accountState, string? selfId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selfId);

        if (accountState.SelfInfo != null)
        {
            return new SelfInfoResult { IsSuccess = true, SelfInfo = accountState.SelfInfo };
        }

        var result = await _oneBotApi.GetLoginInfo(selfId);
        var selfInfo = new SelfInfo
        {
            Nickname = result.Nickname,
            UserId = result.UserId.ToString()
        };
        accountState.SelfInfo = selfInfo;
        return new SelfInfoResult
        {
            IsSuccess = true,
            SelfInfo = selfInfo
        };
    }

    private async Task<ChannelInfoResult> TryGetOrAddChannelInfoCore(AccountContactsState accountState, string channelId,
        string? subChannelId, string? selfId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selfId);

        if (TryGetChannelOrSubChannel(accountState, channelId, subChannelId, out var cachedChannel))
        {
            return new ChannelInfoResult
            {
                IsSuccess = true,
                ChannelInfo = cachedChannel
            };
        }

        if (subChannelId != null)
        {
            // todo: guild
            return ChannelInfoResult.Fail;
        }

        try
        {
            var groupInfo = await _oneBotApi.GetGroupInfo(long.Parse(channelId), selfId);
            var channelInfo = new ChannelInfo(channelId)
            {
                Name = string.IsNullOrEmpty(groupInfo.GroupName) ? null : groupInfo.GroupName
            };
            accountState.ChannelMapping.AddOrUpdate(channelInfo.ChannelId, channelInfo, (_, _) => channelInfo);
            return new ChannelInfoResult { IsSuccess = true, ChannelInfo = channelInfo };
        }
        catch (OneBotApiException ex)
        {
            _logger.LogWarning("获取群信息时API返回错误：" + ex.Message);
            return ChannelInfoResult.Fail;
        }
    }

    private async Task<MemberInfoResult> TryGetOrAddMemberInfoCore(AccountContactsState accountState, string channelId,
        string userId, string? subChannelId, string? selfId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selfId);

        if (TryGetMember(accountState, channelId, userId, subChannelId, out var cachedMember))
        {
            return new MemberInfoResult
            {
                IsSuccess = true,
                MemberInfo = cachedMember
            };
        }

        GroupMember groupMember;
        try
        {
            groupMember = await _oneBotApi.GetGroupMemberDetail(long.Parse(channelId), long.Parse(userId), selfId, false);
        }
        catch (OneBotApiException ex)
        {
            _logger.LogWarning("获取群成员信息时API返回错误：" + ex.Message);
            return MemberInfoResult.Fail;
        }

        var memberInfo = new MemberInfo(channelId, groupMember.UserId, subChannelId)
        {
            Nickname = string.IsNullOrEmpty(groupMember.Nickname) ? null : groupMember.Nickname,
            Card = string.IsNullOrEmpty(groupMember.Card) ? null : groupMember.Card,
            MemberRole = groupMember.Role switch
            {
                "owner" => MemberRole.Owner,
                "admin" => MemberRole.Admin,
                _ => MemberRole.Member
            }
        };

        var channelResult = await TryGetOrAddChannelInfoCore(accountState, channelId, subChannelId, selfId);
        if (!channelResult.IsSuccess || channelResult.ChannelInfo == null)
        {
            return MemberInfoResult.Fail;
        }

        channelResult.ChannelInfo.Members.AddOrUpdate(memberInfo.UserId, memberInfo, (_, _) => memberInfo);
        return new MemberInfoResult { IsSuccess = true, MemberInfo = memberInfo };
    }

    private async Task<PrivateInfoResult> TryGetOrAddPrivateInfoCore(AccountContactsState accountState, string userId,
        string? selfId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selfId);

        if (accountState.PrivateMapping.TryGetValue(userId, out var privateInfo))
        {
            return new PrivateInfoResult
            {
                IsSuccess = true,
                PrivateInfo = privateInfo
            };
        }

        try
        {
            var stranger = await _oneBotApi.GetStrangerInfo(long.Parse(userId), selfId, false);
            privateInfo = new PrivateInfo(userId)
            {
                Nickname = string.IsNullOrEmpty(stranger.Nickname) ? null : stranger.Nickname,
                Remark = stranger.Nickname
            };
            accountState.PrivateMapping.AddOrUpdate(privateInfo.UserId, privateInfo, (_, _) => privateInfo);
            return new PrivateInfoResult { IsSuccess = true, PrivateInfo = privateInfo };
        }
        catch (OneBotApiException ex)
        {
            _logger.LogWarning("获取私聊用户信息时API返回错误：" + ex.Message);
            return PrivateInfoResult.Fail;
        }
    }

    private static bool TryGetChannelOrSubChannel(AccountContactsState accountState, string channelId, string? subChannelId,
        [NotNullWhen(true)] out ChannelInfo? channelInfo)
    {
        if (subChannelId == null)
        {
            return accountState.ChannelMapping.TryGetValue(channelId, out channelInfo);
        }

        channelInfo = null;
        return accountState.SubChannelMapping.TryGetValue(channelId, out var subChannels) &&
               subChannels.TryGetValue(subChannelId, out channelInfo);
    }

    private static bool TryGetMember(AccountContactsState accountState, string channelId, string userId,
        string? subChannelId, [NotNullWhen(true)] out MemberInfo? memberInfo)
    {
        memberInfo = null;
        if (!TryGetChannelOrSubChannel(accountState, channelId, subChannelId, out var channelInfo))
        {
            return false;
        }

        return channelInfo.Members.TryGetValue(userId, out memberInfo);
    }

    private AccountContactsState GetAccountState(string accountKey)
    {
        return _accounts.GetOrAdd(accountKey, _ => new AccountContactsState());
    }

    private string GetAccountKey(MessageContext messageContext)
    {
        return messageContext.SelfId
               ?? throw new InvalidOperationException("OneBot multi-account mode requires self_id in MessageContext.");
    }

    private sealed class AccountContactsState
    {
        public SelfInfo? SelfInfo { get; set; }
        public ConcurrentDictionary<string, ConcurrentDictionary<string, ChannelInfo>> SubChannelMapping { get; } = new();
        public ConcurrentDictionary<string, ChannelInfo> ChannelMapping { get; } = new();
        public ConcurrentDictionary<string, PrivateInfo> PrivateMapping { get; } = new();
    }
}