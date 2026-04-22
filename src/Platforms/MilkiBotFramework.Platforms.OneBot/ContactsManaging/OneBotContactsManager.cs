using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.OneBot.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel.Guild;
using MilkiBotFramework.Platforms.OneBot.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.OneBot.ContactsManaging;

public sealed partial class OneBotContactsManager : ContactsManagerBase
{
    public override string PlatformId => PlatformIds.OneBot;

    private readonly OneBotApi _oneBotApi;
    private readonly BotTaskScheduler _botTaskScheduler;
    private readonly ILogger<OneBotContactsManager> _logger;
    private readonly ConcurrentDictionary<string, AccountContactsState> _accounts =
        new(StringComparer.OrdinalIgnoreCase);

    public OneBotContactsManager(OneBotApi oneBotApi,
        BotTaskScheduler botTaskScheduler,
        ILogger<OneBotContactsManager> logger)
        : base(botTaskScheduler, logger)
    {
        _oneBotApi = oneBotApi;
        _botTaskScheduler = botTaskScheduler;
        _logger = logger;
    }

    protected override void InitializeTasksCore()
    {
        _botTaskScheduler.AddTask(RefreshContactsTaskName, builder => builder
            .ByInterval(TimeSpan.FromMinutes(5))
            .AtStartup()
            .DoAsync(RefreshKnownAccountsAsync));
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
        return TryGetOrAddChannelInfoCore(GetAccountState(GetAccountKey(messageContext)), channelId, subChannelId,
            messageContext.SelfId);
    }

    public override Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null)
    {
        throw new NotSupportedException("OneBot multi-account mode requires MessageContext/self_id.");
    }

    public override Task<MemberInfoResult> TryGetOrAddMemberInfo(MessageContext messageContext, string channelId,
        string userId, string? subChannelId = null)
    {
        return TryGetOrAddMemberInfoCore(GetAccountState(GetAccountKey(messageContext)), channelId, userId, subChannelId,
            messageContext.SelfId);
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
        var members = new Dictionary<string, MemberInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var account in _accounts.Values)
        {
            if (!TryGetChannelOrSubChannel(account, channelId, subChannelId, out var channelInfo))
            {
                continue;
            }

            foreach (var member in channelInfo.Members.Values)
            {
                members[member.UserId] = member;
            }
        }

        return members.Values;
    }

    public override IEnumerable<PrivateInfo> GetAllPrivates()
    {
        return _accounts.Values.SelectMany(account => account.PrivateMapping.Values);
    }

    public override async Task HandleMessageAsync(MessageContext messageContext)
    {
        if (messageContext.MessageIdentity?.MessageType != MessageType.Notice)
        {
            return;
        }

        if (!GetContactsUpdateInfo(messageContext, out var updateInfo))
        {
            return;
        }

        var accountState = GetAccountState(GetAccountKey(messageContext));
        await ApplyContactsUpdateAsync(accountState, updateInfo!, messageContext.SelfId);
    }

    protected override bool GetContactsUpdateInfo(MessageContext messageContext, [NotNullWhen(true)] out ContactsUpdateInfo? updateInfo)
    {
        if (messageContext is not OneBotMessageContext oneBotMessageContext)
        {
            updateInfo = null;
            return false;
        }

        var root = oneBotMessageContext.RawJsonDocument.RootElement;
        if (!TryGetString(root, "notice_type", out var noticeType))
        {
            updateInfo = null;
            return false;
        }

        updateInfo = noticeType switch
        {
            "friend_add" => TryCreateFriendAddUpdate(root),
            "group_increase" => TryCreateGroupMemberChangeUpdate(root, messageContext.SelfId, true),
            "group_decrease" => TryCreateGroupMemberChangeUpdate(root, messageContext.SelfId, false),
            "group_admin" => TryCreateGroupAdminUpdate(root),
            "group_name" => TryCreateGroupNameUpdate(root),
            "group_card" => TryCreateGroupCardUpdate(root),
            _ => null
        };

        return updateInfo != null;
    }

    protected override bool GetContactsCore(
        out Dictionary<string, ChannelInfo> channels,
        out Dictionary<string, ChannelInfo> subChannels,
        out Dictionary<string, PrivateInfo> privates)
    {
        channels = _accounts.Values
            .SelectMany(account => account.ChannelMapping)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

        subChannels = _accounts.Values
            .SelectMany(account => account.SubChannelMapping.Values)
            .SelectMany(dict => dict)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

        privates = _accounts.Values
            .SelectMany(account => account.PrivateMapping)
            .GroupBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

        return true;
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
            return await TryGetOrAddGuildSubChannelInfoCore(accountState, channelId, subChannelId, selfId);
        }

        if (!long.TryParse(channelId, out var parsedChannelId))
        {
            return ChannelInfoResult.Fail;
        }

        try
        {
            var groupInfo = await _oneBotApi.GetGroupInfo(parsedChannelId, selfId);
            var channelInfo = new ChannelInfo(channelId)
            {
                Name = string.IsNullOrEmpty(groupInfo.GroupName) ? null : groupInfo.GroupName
            };
            accountState.ChannelMapping.AddOrUpdate(channelInfo.ChannelId, channelInfo, (_, _) => channelInfo);
            return new ChannelInfoResult { IsSuccess = true, ChannelInfo = channelInfo };
        }
        catch (OneBotApiException ex)
        {
            _logger.LogDebug(ex, "Failed to resolve group info for {ChannelId}, fallback to guild lookup.", channelId);
            return await TryGetOrAddGuildRootChannelInfoCore(accountState, channelId, selfId);
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

        if (subChannelId != null)
        {
            return await TryGetOrAddGuildMemberInfoCore(accountState, channelId, userId, subChannelId, selfId);
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

        var memberInfo = CreateGroupMemberInfo(channelId, groupMember.UserId, subChannelId, groupMember);

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

    private async Task RefreshKnownAccountsAsync(TaskContext context, CancellationToken token)
    {
        var accountIds = _accounts.Keys.ToArray();
        foreach (var accountId in accountIds)
        {
            token.ThrowIfCancellationRequested();

            try
            {
                await RefreshAccountStateAsync(accountId, token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to refresh contacts for OneBot account {AccountId}.", accountId);
            }
        }
    }

    private async Task RefreshAccountStateAsync(string accountId, CancellationToken token)
    {
        var snapshot = await BuildAccountSnapshotAsync(accountId, token);
        var accountState = GetAccountState(accountId);
        accountState.SelfInfo = snapshot.SelfInfo;
        accountState.ChannelMapping = snapshot.ChannelMapping;
        accountState.SubChannelMapping = snapshot.SubChannelMapping;
        accountState.PrivateMapping = snapshot.PrivateMapping;
    }

    private async Task<AccountContactsState> BuildAccountSnapshotAsync(string accountId, CancellationToken token)
    {
        var snapshot = new AccountContactsState();

        var selfInfo = await _oneBotApi.GetLoginInfo(accountId);
        snapshot.SelfInfo = new SelfInfo
        {
            Nickname = selfInfo.Nickname,
            UserId = selfInfo.UserId.ToString()
        };

        var friends = await _oneBotApi.GetFriends(accountId);
        foreach (var friend in friends)
        {
            snapshot.PrivateMapping[friend.UserId] = new PrivateInfo(friend.UserId)
            {
                Nickname = string.IsNullOrWhiteSpace(friend.Nickname) ? null : friend.Nickname,
                Remark = string.IsNullOrWhiteSpace(friend.Remark) ? null : friend.Remark
            };
        }

        var groups = await _oneBotApi.GetGroups(accountId);
        foreach (var group in groups)
        {
            token.ThrowIfCancellationRequested();

            var channelInfo = new ChannelInfo(group.GroupId)
            {
                Name = string.IsNullOrWhiteSpace(group.GroupName) ? null : group.GroupName
            };

            var members = await _oneBotApi.GetFuzzyGroupMembers(long.Parse(group.GroupId), accountId);
            foreach (var member in members)
            {
                channelInfo.Members[member.UserId] = CreateGroupMemberInfo(group.GroupId, member.UserId, null, member);
            }

            snapshot.ChannelMapping[group.GroupId] = channelInfo;
        }

        try
        {
            var guilds = await _oneBotApi.GetGuilds(accountId);
            foreach (var guild in guilds)
            {
                token.ThrowIfCancellationRequested();

                var guildId = guild.GuildId.ToString();
                var guildMembers = await SafeGetGuildMembersAsync(guild.GuildId, accountId);
                snapshot.ChannelMapping[guildId] = new ChannelInfo(guildId, CreateGuildMembers(guildId, null, guildMembers))
                {
                    Name = string.IsNullOrWhiteSpace(guild.GuildName) ? null : guild.GuildName
                };

                var channels = await _oneBotApi.GetGuildChannelList(guild.GuildId, accountId);
                var subChannelMap = new ConcurrentDictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var channel in channels.Where(channel => channel.ChannelType == ChannelType.Text))
                {
                    var subChannelId = channel.ChannelId.ToString();
                    subChannelMap[subChannelId] = new ChannelInfo(guildId,
                        CreateGuildMembers(guildId, subChannelId, guildMembers))
                    {
                        SubChannelId = subChannelId,
                        Name = string.IsNullOrWhiteSpace(channel.ChannelName) ? null : channel.ChannelName
                    };
                }

                if (subChannelMap.Count > 0)
                {
                    snapshot.SubChannelMapping[guildId] = subChannelMap;
                }
            }
        }
        catch (OneBotApiException ex)
        {
            _logger.LogDebug(ex, "Skipping guild refresh for OneBot account {AccountId}.", accountId);
        }

        return snapshot;
    }

    private async Task ApplyContactsUpdateAsync(AccountContactsState accountState, ContactsUpdateInfo updateInfo, string? selfId)
    {
        switch (updateInfo.ContactsUpdateRole)
        {
            case ContactsUpdateRole.Channel:
            {
                var channelInfo = await ApplyChannelUpdateAsync(accountState, updateInfo, selfId);
                await NotifyContactsUpdatedAsync((ContactsUpdateEvent)(ContactsUpdateSingleEvent)new ContactsUpdateSingleEvent
                {
                    ChannelInfo = channelInfo,
                    ChangedPath = ResolveChangedPath(updateInfo),
                    UpdateRole = updateInfo.ContactsUpdateRole,
                    UpdateType = updateInfo.ContactsUpdateType
                });
                break;
            }
            case ContactsUpdateRole.SubChannel:
            {
                var subChannelInfo = await ApplySubChannelUpdateAsync(accountState, updateInfo, selfId);
                await NotifyContactsUpdatedAsync((ContactsUpdateEvent)(ContactsUpdateSingleEvent)new ContactsUpdateSingleEvent
                {
                    SubChannelInfo = subChannelInfo,
                    ChangedPath = ResolveChangedPath(updateInfo),
                    UpdateRole = updateInfo.ContactsUpdateRole,
                    UpdateType = updateInfo.ContactsUpdateType
                });
                break;
            }
            case ContactsUpdateRole.Member:
            {
                var memberInfo = await ApplyMemberUpdateAsync(accountState, updateInfo, selfId);
                await NotifyContactsUpdatedAsync((ContactsUpdateEvent)(ContactsUpdateSingleEvent)new ContactsUpdateSingleEvent
                {
                    MemberInfo = memberInfo,
                    ChangedPath = ResolveChangedPath(updateInfo),
                    UpdateRole = updateInfo.ContactsUpdateRole,
                    UpdateType = updateInfo.ContactsUpdateType
                });
                break;
            }
            case ContactsUpdateRole.Private:
            {
                var privateInfo = await ApplyPrivateUpdateAsync(accountState, updateInfo, selfId);
                await NotifyContactsUpdatedAsync((ContactsUpdateEvent)(ContactsUpdateSingleEvent)new ContactsUpdateSingleEvent
                {
                    PrivateInfo = privateInfo,
                    ChangedPath = ResolveChangedPath(updateInfo),
                    UpdateRole = updateInfo.ContactsUpdateRole,
                    UpdateType = updateInfo.ContactsUpdateType
                });
                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private async Task<ChannelInfo?> ApplyChannelUpdateAsync(AccountContactsState accountState, ContactsUpdateInfo updateInfo,
        string? selfId)
    {
        if (updateInfo.ContactsUpdateType == ContactsUpdateType.Removed)
        {
            accountState.SubChannelMapping.TryRemove(updateInfo.Id, out _);
            accountState.ChannelMapping.TryRemove(updateInfo.Id, out var removedChannel);
            return removedChannel;
        }

        ChannelInfo? channelInfo = null;
        if (!string.IsNullOrWhiteSpace(selfId))
        {
            var fetchedChannel = await TryGetOrAddChannelInfoCore(accountState, updateInfo.Id, null, selfId);
            if (fetchedChannel.IsSuccess)
            {
                channelInfo = fetchedChannel.ChannelInfo;
            }
        }

        channelInfo ??= accountState.ChannelMapping.AddOrUpdate(updateInfo.Id,
            id => new ChannelInfo(id)
            {
                Name = updateInfo.Name
            },
            (_, existing) =>
            {
                if (!string.IsNullOrWhiteSpace(updateInfo.Name))
                {
                    existing.Name = updateInfo.Name;
                }

                return existing;
            });

        if (!string.IsNullOrWhiteSpace(updateInfo.Name))
        {
            channelInfo.Name = updateInfo.Name;
        }

        return channelInfo;
    }

    private async Task<ChannelInfo?> ApplySubChannelUpdateAsync(AccountContactsState accountState, ContactsUpdateInfo updateInfo,
        string? selfId)
    {
        if (updateInfo.SubId == null)
        {
            return null;
        }

        var subChannelMap = accountState.SubChannelMapping.GetOrAdd(updateInfo.Id,
            _ => new ConcurrentDictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase));

        if (updateInfo.ContactsUpdateType == ContactsUpdateType.Removed)
        {
            subChannelMap.TryRemove(updateInfo.SubId, out var removedSubChannel);
            return removedSubChannel;
        }

        ChannelInfo? subChannelInfo = null;
        if (!string.IsNullOrWhiteSpace(selfId))
        {
            var fetchedSubChannel = await TryGetOrAddChannelInfoCore(accountState, updateInfo.Id, updateInfo.SubId, selfId);
            if (fetchedSubChannel.IsSuccess)
            {
                subChannelInfo = fetchedSubChannel.ChannelInfo;
            }
        }

        subChannelInfo ??= subChannelMap.AddOrUpdate(updateInfo.SubId,
            id => new ChannelInfo(updateInfo.Id)
            {
                SubChannelId = id,
                Name = updateInfo.Name
            },
            (_, existing) =>
            {
                if (!string.IsNullOrWhiteSpace(updateInfo.Name))
                {
                    existing.Name = updateInfo.Name;
                }

                return existing;
            });

        if (!string.IsNullOrWhiteSpace(updateInfo.Name))
        {
            subChannelInfo.Name = updateInfo.Name;
        }

        return subChannelInfo;
    }

    private async Task<MemberInfo?> ApplyMemberUpdateAsync(AccountContactsState accountState, ContactsUpdateInfo updateInfo,
        string? selfId)
    {
        if (updateInfo.UserId == null)
        {
            return null;
        }

        var channelInfo = await EnsureChannelForMemberUpdateAsync(accountState, updateInfo, selfId);
        if (channelInfo == null)
        {
            return null;
        }

        if (updateInfo.ContactsUpdateType == ContactsUpdateType.Removed)
        {
            channelInfo.Members.TryRemove(updateInfo.UserId, out var removedMember);
            return removedMember;
        }

        MemberInfo? memberInfo = null;
        if (!string.IsNullOrWhiteSpace(selfId))
        {
            var fetchedMember = await TryGetOrAddMemberInfoCore(accountState, updateInfo.Id, updateInfo.UserId,
                updateInfo.SubId, selfId);
            if (fetchedMember.IsSuccess)
            {
                memberInfo = fetchedMember.MemberInfo;
            }
        }

        memberInfo ??= channelInfo.Members.AddOrUpdate(updateInfo.UserId,
            userId => new MemberInfo(updateInfo.Id, userId, updateInfo.SubId)
            {
                Nickname = updateInfo.Name,
                Card = updateInfo.Remark,
                MemberRole = updateInfo.MemberRole ?? MemberRole.Member
            },
            (_, existing) =>
            {
                if (!string.IsNullOrWhiteSpace(updateInfo.Name))
                {
                    existing.Nickname = updateInfo.Name;
                }

                if (!string.IsNullOrWhiteSpace(updateInfo.Remark))
                {
                    existing.Card = updateInfo.Remark;
                }

                if (updateInfo.MemberRole.HasValue)
                {
                    existing.MemberRole = updateInfo.MemberRole.Value;
                }

                return existing;
            });

        if (!string.IsNullOrWhiteSpace(updateInfo.Name))
        {
            memberInfo.Nickname = updateInfo.Name;
        }

        if (!string.IsNullOrWhiteSpace(updateInfo.Remark))
        {
            memberInfo.Card = updateInfo.Remark;
        }

        if (updateInfo.MemberRole.HasValue)
        {
            memberInfo.MemberRole = updateInfo.MemberRole.Value;
        }

        channelInfo.Members[updateInfo.UserId] = memberInfo;
        return memberInfo;
    }

    private async Task<PrivateInfo?> ApplyPrivateUpdateAsync(AccountContactsState accountState, ContactsUpdateInfo updateInfo,
        string? selfId)
    {
        if (updateInfo.ContactsUpdateType == ContactsUpdateType.Removed)
        {
            accountState.PrivateMapping.TryRemove(updateInfo.Id, out var removedPrivate);
            return removedPrivate;
        }

        PrivateInfo? privateInfo = null;
        if (!string.IsNullOrWhiteSpace(selfId))
        {
            var fetchedPrivate = await TryGetOrAddPrivateInfoCore(accountState, updateInfo.Id, selfId);
            if (fetchedPrivate.IsSuccess)
            {
                privateInfo = fetchedPrivate.PrivateInfo;
            }
        }

        privateInfo ??= accountState.PrivateMapping.AddOrUpdate(updateInfo.Id,
            id => new PrivateInfo(id)
            {
                Nickname = updateInfo.Name,
                Remark = updateInfo.Remark
            },
            (_, existing) =>
            {
                if (!string.IsNullOrWhiteSpace(updateInfo.Name))
                {
                    existing.Nickname = updateInfo.Name;
                }

                if (!string.IsNullOrWhiteSpace(updateInfo.Remark))
                {
                    existing.Remark = updateInfo.Remark;
                }

                return existing;
            });

        if (!string.IsNullOrWhiteSpace(updateInfo.Name))
        {
            privateInfo.Nickname = updateInfo.Name;
        }

        if (!string.IsNullOrWhiteSpace(updateInfo.Remark))
        {
            privateInfo.Remark = updateInfo.Remark;
        }

        return privateInfo;
    }

    private async Task<ChannelInfo?> EnsureChannelForMemberUpdateAsync(AccountContactsState accountState,
        ContactsUpdateInfo updateInfo,
        string? selfId)
    {
        if (TryGetChannelOrSubChannel(accountState, updateInfo.Id, updateInfo.SubId, out var existingChannel))
        {
            return existingChannel;
        }

        if (!string.IsNullOrWhiteSpace(selfId))
        {
            var channelResult = await TryGetOrAddChannelInfoCore(accountState, updateInfo.Id, updateInfo.SubId, selfId);
            if (channelResult.IsSuccess)
            {
                return channelResult.ChannelInfo;
            }
        }

        if (updateInfo.SubId == null)
        {
            return accountState.ChannelMapping.AddOrUpdate(updateInfo.Id,
                id => new ChannelInfo(id),
                (_, current) => current);
        }

        var subChannelMap = accountState.SubChannelMapping.GetOrAdd(updateInfo.Id,
            _ => new ConcurrentDictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase));
        return subChannelMap.AddOrUpdate(updateInfo.SubId,
            subId => new ChannelInfo(updateInfo.Id)
            {
                SubChannelId = subId
            },
            (_, current) => current);
    }

    private async Task<ChannelInfoResult> TryGetOrAddGuildRootChannelInfoCore(AccountContactsState accountState,
        string channelId,
        string selfId)
    {
        if (accountState.ChannelMapping.TryGetValue(channelId, out var cachedChannel))
        {
            return new ChannelInfoResult
            {
                IsSuccess = true,
                ChannelInfo = cachedChannel
            };
        }

        if (!long.TryParse(channelId, out var guildId))
        {
            return ChannelInfoResult.Fail;
        }

        try
        {
            var guildInfo = await _oneBotApi.GetGuildMetaByGuest(guildId, selfId);
            var guildMembers = await SafeGetGuildMembersAsync(guildId, selfId);
            var channelInfo = new ChannelInfo(channelId, CreateGuildMembers(channelId, null, guildMembers))
            {
                Name = string.IsNullOrWhiteSpace(guildInfo.GuildName) ? null : guildInfo.GuildName
            };

            accountState.ChannelMapping[channelId] = channelInfo;
            return new ChannelInfoResult
            {
                IsSuccess = true,
                ChannelInfo = channelInfo
            };
        }
        catch (OneBotApiException ex)
        {
            _logger.LogWarning("获取频道信息时API返回错误：" + ex.Message);
            return ChannelInfoResult.Fail;
        }
    }

    private async Task<ChannelInfoResult> TryGetOrAddGuildSubChannelInfoCore(AccountContactsState accountState,
        string channelId,
        string subChannelId,
        string selfId)
    {
        if (TryGetChannelOrSubChannel(accountState, channelId, subChannelId, out var cachedSubChannel))
        {
            return new ChannelInfoResult
            {
                IsSuccess = true,
                ChannelInfo = cachedSubChannel
            };
        }

        if (!long.TryParse(channelId, out var guildId))
        {
            return ChannelInfoResult.Fail;
        }

        await TryGetOrAddGuildRootChannelInfoCore(accountState, channelId, selfId);

        try
        {
            var guildMembers = await SafeGetGuildMembersAsync(guildId, selfId);
            var channels = await _oneBotApi.GetGuildChannelList(guildId, selfId);
            var subChannel = channels.FirstOrDefault(channel =>
                channel.ChannelType == ChannelType.Text &&
                string.Equals(channel.ChannelId.ToString(), subChannelId, StringComparison.OrdinalIgnoreCase));
            if (subChannel == null)
            {
                return ChannelInfoResult.Fail;
            }

            var subChannelInfo = new ChannelInfo(channelId, CreateGuildMembers(channelId, subChannelId, guildMembers))
            {
                SubChannelId = subChannelId,
                Name = string.IsNullOrWhiteSpace(subChannel.ChannelName) ? null : subChannel.ChannelName
            };

            var subChannelMap = accountState.SubChannelMapping.GetOrAdd(channelId,
                _ => new ConcurrentDictionary<string, ChannelInfo>(StringComparer.OrdinalIgnoreCase));
            subChannelMap[subChannelId] = subChannelInfo;
            return new ChannelInfoResult
            {
                IsSuccess = true,
                ChannelInfo = subChannelInfo
            };
        }
        catch (OneBotApiException ex)
        {
            _logger.LogWarning("获取子频道信息时API返回错误：" + ex.Message);
            return ChannelInfoResult.Fail;
        }
    }

    private async Task<MemberInfoResult> TryGetOrAddGuildMemberInfoCore(AccountContactsState accountState, string channelId,
        string userId, string subChannelId, string selfId)
    {
        var channelResult = await TryGetOrAddGuildSubChannelInfoCore(accountState, channelId, subChannelId, selfId);
        if (!channelResult.IsSuccess || channelResult.ChannelInfo == null)
        {
            return MemberInfoResult.Fail;
        }

        try
        {
            var guildMembers = await SafeGetGuildMembersAsync(long.Parse(channelId), selfId);
            var memberInfo = CreateGuildMembers(channelId, subChannelId, guildMembers)
                .FirstOrDefault(member => string.Equals(member.UserId, userId, StringComparison.OrdinalIgnoreCase));
            if (memberInfo == null)
            {
                return MemberInfoResult.Fail;
            }

            channelResult.ChannelInfo.Members[memberInfo.UserId] = memberInfo;
            return new MemberInfoResult
            {
                IsSuccess = true,
                MemberInfo = memberInfo
            };
        }
        catch (OneBotApiException ex)
        {
            _logger.LogWarning("获取频道成员信息时API返回错误：" + ex.Message);
            return MemberInfoResult.Fail;
        }
    }

    private async Task<GetGuildMembersResponse?> SafeGetGuildMembersAsync(long guildId, string selfId)
    {
        try
        {
            return await _oneBotApi.GetGuildMembers(guildId, selfId);
        }
        catch (OneBotApiException ex)
        {
            _logger.LogDebug(ex, "Failed to fetch guild members for {GuildId}.", guildId);
            return null;
        }
    }

    private static List<MemberInfo> CreateGuildMembers(string channelId, string? subChannelId,
        GetGuildMembersResponse? guildMembersResponse)
    {
        if (guildMembersResponse == null)
        {
            return [];
        }

        var members = new Dictionary<string, MemberInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var admin in guildMembersResponse.Admins ?? [])
        {
            var memberInfo = CreateGuildMemberInfo(channelId, subChannelId, admin, MemberRole.Admin);
            members[memberInfo.UserId] = memberInfo;
        }

        foreach (var member in guildMembersResponse.Members ?? [])
        {
            var memberInfo = CreateGuildMemberInfo(channelId, subChannelId, member, MemberRole.Member);
            members[memberInfo.UserId] = memberInfo;
        }

        foreach (var bot in guildMembersResponse.Bots ?? [])
        {
            var memberInfo = CreateGuildMemberInfo(channelId, subChannelId, bot, MemberRole.Member);
            members[memberInfo.UserId] = memberInfo;
        }

        return members.Values.ToList();
    }

    private static MemberInfo CreateGuildMemberInfo(string channelId, string? subChannelId, GuildMember guildMember,
        MemberRole memberRole)
    {
        var userId = guildMember.TinyId.ToString();
        return new MemberInfo(channelId, userId, subChannelId)
        {
            Nickname = string.IsNullOrWhiteSpace(guildMember.Nickname) ? null : guildMember.Nickname,
            Card = string.IsNullOrWhiteSpace(guildMember.Title) ? null : guildMember.Title,
            MemberRole = memberRole
        };
    }

    private static MemberInfo CreateGroupMemberInfo(string channelId, string userId, string? subChannelId, GroupMember groupMember)
    {
        return new MemberInfo(channelId, userId, subChannelId)
        {
            Nickname = string.IsNullOrWhiteSpace(groupMember.Nickname) ? null : groupMember.Nickname,
            Card = string.IsNullOrWhiteSpace(groupMember.Card) ? null : groupMember.Card,
            MemberRole = groupMember.Role switch
            {
                "owner" => MemberRole.Owner,
                "admin" => MemberRole.Admin,
                _ => MemberRole.Member
            }
        };
    }

    private static ContactsUpdateInfo? TryCreateFriendAddUpdate(JsonElement root)
    {
        return TryGetId(root, "user_id", out var userId)
            ? new ContactsUpdateInfo(userId)
            {
                ContactsUpdateRole = ContactsUpdateRole.Private,
                ContactsUpdateType = ContactsUpdateType.Added
            }
            : null;
    }

    private static ContactsUpdateInfo? TryCreateGroupMemberChangeUpdate(JsonElement root, string? selfId, bool isIncrease)
    {
        if (!TryGetId(root, "group_id", out var groupId) || !TryGetId(root, "user_id", out var userId))
        {
            return null;
        }

        var isSelfChange = !string.IsNullOrWhiteSpace(selfId) &&
                           string.Equals(selfId, userId, StringComparison.OrdinalIgnoreCase);
        return new ContactsUpdateInfo(groupId)
        {
            UserId = isSelfChange ? null : userId,
            ContactsUpdateRole = isSelfChange ? ContactsUpdateRole.Channel : ContactsUpdateRole.Member,
            ContactsUpdateType = isIncrease ? ContactsUpdateType.Added : ContactsUpdateType.Removed
        };
    }

    private static ContactsUpdateInfo? TryCreateGroupAdminUpdate(JsonElement root)
    {
        if (!TryGetId(root, "group_id", out var groupId) ||
            !TryGetId(root, "user_id", out var userId) ||
            !TryGetString(root, "sub_type", out var subType))
        {
            return null;
        }

        return new ContactsUpdateInfo(groupId)
        {
            UserId = userId,
            ContactsUpdateRole = ContactsUpdateRole.Member,
            ContactsUpdateType = ContactsUpdateType.Changed,
            MemberRole = string.Equals(subType, "set", StringComparison.OrdinalIgnoreCase)
                ? MemberRole.Admin
                : MemberRole.Member
        };
    }

    private static ContactsUpdateInfo? TryCreateGroupNameUpdate(JsonElement root)
    {
        if (!TryGetId(root, "group_id", out var groupId))
        {
            return null;
        }

        var name = TryGetOptionalString(root, "group_name")
                   ?? TryGetOptionalString(root, "name")
                   ?? TryGetOptionalString(root, "new_name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return new ContactsUpdateInfo(groupId)
        {
            Name = name,
            ContactsUpdateRole = ContactsUpdateRole.Channel,
            ContactsUpdateType = ContactsUpdateType.Changed
        };
    }

    private static ContactsUpdateInfo? TryCreateGroupCardUpdate(JsonElement root)
    {
        if (!TryGetId(root, "group_id", out var groupId) || !TryGetId(root, "user_id", out var userId))
        {
            return null;
        }

        var card = TryGetOptionalString(root, "card_new")
                   ?? TryGetOptionalString(root, "card");
        if (card == null)
        {
            return null;
        }

        return new ContactsUpdateInfo(groupId)
        {
            UserId = userId,
            Remark = card,
            ContactsUpdateRole = ContactsUpdateRole.Member,
            ContactsUpdateType = ContactsUpdateType.Changed
        };
    }

    private static string? ResolveChangedPath(ContactsUpdateInfo updateInfo)
    {
        if (updateInfo.ContactsUpdateType != ContactsUpdateType.Changed)
        {
            return null;
        }

        return updateInfo.ContactsUpdateRole switch
        {
            ContactsUpdateRole.Channel when updateInfo.Name != null => nameof(ChannelInfo.Name),
            ContactsUpdateRole.SubChannel when updateInfo.Name != null => nameof(ChannelInfo.Name),
            ContactsUpdateRole.Member when updateInfo.MemberRole.HasValue => nameof(MemberInfo.MemberRole),
            ContactsUpdateRole.Member when updateInfo.Remark != null => nameof(MemberInfo.Card),
            ContactsUpdateRole.Member when updateInfo.Name != null => nameof(MemberInfo.Nickname),
            ContactsUpdateRole.Private when updateInfo.Remark != null => nameof(PrivateInfo.Remark),
            ContactsUpdateRole.Private when updateInfo.Name != null => nameof(PrivateInfo.Nickname),
            _ => null
        };
    }

    private static bool TryGetString(JsonElement root, string propertyName, [NotNullWhen(true)] out string? value)
    {
        value = TryGetOptionalString(root, propertyName);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static string? TryGetOptionalString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return null;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            _ => null
        };
    }

    private static bool TryGetId(JsonElement root, string propertyName, [NotNullWhen(true)] out string? value)
    {
        value = TryGetOptionalString(root, propertyName);
        return !string.IsNullOrWhiteSpace(value);
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
        public ConcurrentDictionary<string, ConcurrentDictionary<string, ChannelInfo>> SubChannelMapping { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentDictionary<string, ChannelInfo> ChannelMapping { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public ConcurrentDictionary<string, PrivateInfo> PrivateMapping { get; set; } =
            new(StringComparer.OrdinalIgnoreCase);
    }
}
