using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.QQ.Connecting;
using MilkiBotFramework.Platforms.QQ.Connecting.Models;
using MilkiBotFramework.Platforms.QQ.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.QQ.ContactsManaging;

public sealed class QContactsManager : ContactsManagerBase
{
    private const int MissingPermissionCode = 11253;

    private readonly IQGroupMemberApi? _groupMemberApi;
    private readonly BotTaskScheduler _botTaskScheduler;
    private readonly ILogger<QContactsManager> _logger;
    private int _memberApiUnavailable;

    public override string PlatformId => PlatformIds.Qq;

    public QContactsManager(
        QApi qApi,
        BotTaskScheduler botTaskScheduler,
        ILogger<QContactsManager> logger)
        : this((IQGroupMemberApi)qApi, botTaskScheduler, logger)
    {
    }

    internal QContactsManager(
        IQGroupMemberApi? groupMemberApi,
        BotTaskScheduler botTaskScheduler,
        ILogger<QContactsManager> logger)
        : base(botTaskScheduler, logger)
    {
        _groupMemberApi = groupMemberApi;
        _botTaskScheduler = botTaskScheduler;
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

        return new SelfInfoResult { IsSuccess = true, SelfInfo = SelfInfo };
    }

    public override async Task<ChannelInfoResult> TryGetOrAddChannelInfo(string channelId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddChannelInfo(channelId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;

        var channelInfo = ChannelMapping.GetOrAdd(channelId, static id => new ChannelInfo(id));
        return new ChannelInfoResult { IsSuccess = true, ChannelInfo = channelInfo };
    }

    public override async Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddMemberInfo(channelId, userId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;

        var channelInfo = await EnsureChannelInfoAsync(channelId, subChannelId);
        if (channelInfo == null)
        {
            return MemberInfoResult.Fail;
        }

        var apiMember = await TryFetchGroupMemberFromApiAsync(channelId, userId);
        var memberInfo = new MemberInfo(channelId, userId, subChannelId)
        {
            Nickname = apiMember?.Username,
            Card = apiMember?.MemberNickname,
            MemberRole = apiMember?.Role ?? MemberRole.Member
        };

        var cachedMember = channelInfo.Members.GetOrAdd(userId, memberInfo);
        if (ReferenceEquals(cachedMember, memberInfo))
        {
            await NotifyContactsUpdatedAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
            {
                MemberInfo = cachedMember,
                UpdateRole = ContactsUpdateRole.Member,
                UpdateType = ContactsUpdateType.Added
            });
        }

        return new MemberInfoResult { IsSuccess = true, MemberInfo = cachedMember };
    }

    public override async Task<MemberInfoResult> TryGetOrAddMemberInfo(MessageContext messageContext, string channelId,
        string userId, string? subChannelId = null)
    {
        if (messageContext is not QMessageContext qContext ||
            qContext.MessageIdentity?.MessageType != MessageType.Channel ||
            !string.Equals(qContext.MessageIdentity.Id, channelId, StringComparison.Ordinal) ||
            !string.Equals(qContext.MessageUserIdentity?.UserId, userId, StringComparison.Ordinal))
        {
            return await TryGetOrAddMemberInfo(channelId, userId, subChannelId);
        }

        var channelInfo = await EnsureChannelInfoAsync(channelId, subChannelId);
        if (channelInfo == null)
        {
            return MemberInfoResult.Fail;
        }

        if (channelInfo.Members.TryGetValue(userId, out var existing))
        {
            await SyncMemberFromMessageAsync(existing, qContext.AuthorUsername, qContext.AuthorMemberNickname, qContext.AuthorMemberRole);
            return new MemberInfoResult { IsSuccess = true, MemberInfo = existing };
        }

        QGroupMember? apiMember = null;
        if (!qContext.AuthorMemberRole.HasValue)
        {
            apiMember = await TryFetchGroupMemberFromApiAsync(channelId, userId);
        }

        var role = qContext.AuthorMemberRole ?? apiMember?.Role ?? MemberRole.Member;
        var nickname = !string.IsNullOrWhiteSpace(qContext.AuthorUsername)
            ? qContext.AuthorUsername
            : apiMember?.Username;
        var card = !string.IsNullOrWhiteSpace(qContext.AuthorMemberNickname)
            ? qContext.AuthorMemberNickname
            : apiMember?.MemberNickname;

        var newMember = new MemberInfo(channelId, userId, subChannelId)
        {
            Nickname = nickname,
            Card = card,
            MemberRole = role
        };

        if (channelInfo.Members.TryAdd(userId, newMember))
        {
            await NotifyContactsUpdatedAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
            {
                MemberInfo = newMember,
                UpdateRole = ContactsUpdateRole.Member,
                UpdateType = ContactsUpdateType.Added
            });

            return new MemberInfoResult { IsSuccess = true, MemberInfo = newMember };
        }

        var racedMember = channelInfo.Members[userId];
        await SyncMemberFromMessageAsync(racedMember, nickname, card, qContext.AuthorMemberRole ?? apiMember?.Role);
        return new MemberInfoResult { IsSuccess = true, MemberInfo = racedMember };
    }

    public override async Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        var baseResult = await base.TryGetOrAddPrivateInfo(userId);
        if (baseResult.IsSuccess) return baseResult;

        var privateInfo = PrivateMapping.GetOrAdd(userId, static id => new PrivateInfo(id));
        return new PrivateInfoResult { IsSuccess = true, PrivateInfo = privateInfo };
    }

    public override async Task<PrivateInfoResult> TryGetOrAddPrivateInfo(MessageContext messageContext, string userId)
    {
        var result = await TryGetOrAddPrivateInfo(userId);
        if (!result.IsSuccess || result.PrivateInfo == null)
        {
            return result;
        }

        if (messageContext is QMessageContext qContext &&
            qContext.MessageIdentity?.MessageType == MessageType.Private &&
            string.Equals(qContext.MessageUserIdentity?.UserId, userId, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(qContext.AuthorUsername))
        {
            var privateInfo = result.PrivateInfo;
            var changed = false;
            lock (privateInfo)
            {
                if (!string.Equals(privateInfo.Nickname, qContext.AuthorUsername, StringComparison.Ordinal))
                {
                    privateInfo.Nickname = qContext.AuthorUsername;
                    changed = true;
                }
            }

            if (changed)
            {
                await NotifyContactsUpdatedAsync((ContactsUpdateEvent)ContactsUpdateSingleEvent.Update(privateInfo, nameof(PrivateInfo.Nickname)));
            }
        }

        return result;
    }

    /// <summary>
    /// 主动调用 QQ 群成员接口刷新指定成员的信息与角色。
    /// </summary>
    public async Task<MemberInfoResult> TryRefreshMemberInfoAsync(string groupOpenId, string memberOpenId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupOpenId);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberOpenId);

        var apiMember = await TryFetchGroupMemberFromApiAsync(groupOpenId, memberOpenId, cancellationToken);
        if (apiMember == null)
        {
            return MemberInfoResult.Fail;
        }

        var channelInfo = await EnsureChannelInfoAsync(groupOpenId, null);
        if (channelInfo == null)
        {
            return MemberInfoResult.Fail;
        }

        if (channelInfo.Members.TryGetValue(memberOpenId, out var existing))
        {
            await SyncMemberFromMessageAsync(existing, apiMember.Username, apiMember.MemberNickname, apiMember.Role);
            return new MemberInfoResult { IsSuccess = true, MemberInfo = existing };
        }

        var newMember = new MemberInfo(groupOpenId, memberOpenId, null)
        {
            Nickname = apiMember.Username,
            Card = apiMember.MemberNickname,
            MemberRole = apiMember.Role ?? MemberRole.Member
        };

        if (channelInfo.Members.TryAdd(memberOpenId, newMember))
        {
            await NotifyContactsUpdatedAsync((ContactsUpdateEvent)new ContactsUpdateSingleEvent
            {
                MemberInfo = newMember,
                UpdateRole = ContactsUpdateRole.Member,
                UpdateType = ContactsUpdateType.Added
            });
            return new MemberInfoResult { IsSuccess = true, MemberInfo = newMember };
        }

        var racedMember = channelInfo.Members[memberOpenId];
        await SyncMemberFromMessageAsync(racedMember, apiMember.Username, apiMember.MemberNickname, apiMember.Role);
        return new MemberInfoResult { IsSuccess = true, MemberInfo = racedMember };
    }

    /// <summary>
    /// 主动拉取指定 QQ 群的全量成员列表并同步本地联系人缓存（包含新增、退群移除与角色变更）。
    /// </summary>
    public async Task<bool> TryRefreshGroupMembersAsync(string groupOpenId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(groupOpenId);

        if (_groupMemberApi == null || Volatile.Read(ref _memberApiUnavailable) != 0)
        {
            return false;
        }

        IReadOnlyList<QGroupMember> remoteMembers;
        try
        {
            remoteMembers = await _groupMemberApi.GetGroupMembersAsync(groupOpenId, cancellationToken);
        }
        catch (QApiException ex)
        {
            HandleMemberApiException(ex, groupOpenId);
            return false;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "刷新 QQ 群 {GroupOpenId} 成员列表失败。", groupOpenId);
            return false;
        }

        var channelInfo = await EnsureChannelInfoAsync(groupOpenId, null);
        if (channelInfo == null)
        {
            return false;
        }

        var events = ReconcileGroupMembers(channelInfo, remoteMembers);
        if (events.Count > 0)
        {
            await NotifyContactsUpdatedAsync(new ContactsUpdateEvent { Events = events });
        }

        return true;
    }

    protected override bool GetContactsUpdateInfo(MessageContext messageContext, [NotNullWhen(true)] out ContactsUpdateInfo? updateInfo)
    {
        updateInfo = null;
        if (messageContext is not QMessageContext qContext)
        {
            return false;
        }

        var root = qContext.RawJsonDocument.RootElement;
        if (!root.TryGetProperty("t", out var tProp) || tProp.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("d", out var dProp) || dProp.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var eventType = tProp.GetString();
        switch (eventType)
        {
            case "GROUP_ADD_ROBOT":
            case "GROUP_DEL_ROBOT":
            {
                var groupOpenId = TryGetOptionalString(dProp, "group_openid");
                if (string.IsNullOrWhiteSpace(groupOpenId))
                {
                    return false;
                }

                updateInfo = new ContactsUpdateInfo(groupOpenId)
                {
                    ContactsUpdateRole = ContactsUpdateRole.Channel,
                    ContactsUpdateType = eventType == "GROUP_ADD_ROBOT"
                        ? ContactsUpdateType.Added
                        : ContactsUpdateType.Removed
                };
                return true;
            }
            case "FRIEND_ADD":
            case "FRIEND_DEL":
            {
                var openId = TryGetOptionalString(dProp, "openid");
                if (string.IsNullOrWhiteSpace(openId))
                {
                    return false;
                }

                updateInfo = new ContactsUpdateInfo(openId)
                {
                    ContactsUpdateRole = ContactsUpdateRole.Private,
                    ContactsUpdateType = eventType == "FRIEND_ADD"
                        ? ContactsUpdateType.Added
                        : ContactsUpdateType.Removed
                };
                return true;
            }
            default:
                return false;
        }
    }

    protected override bool GetContactsCore(
        [NotNullWhen(true)] out Dictionary<string, ChannelInfo>? channels,
        [NotNullWhen(true)] out Dictionary<string, ChannelInfo>? subChannels,
        [NotNullWhen(true)] out Dictionary<string, PrivateInfo>? privates)
    {
        channels = null;
        subChannels = null;
        privates = null;
        return false;
    }

    protected override void InitializeTasksCore()
    {
        if (_groupMemberApi == null)
        {
            return;
        }

        _botTaskScheduler.AddTask(RefreshContactsTaskName, builder => builder
            .ByInterval(TimeSpan.FromMinutes(5))
            .DoAsync(RefreshKnownGroupsAsync));
    }

    private async Task RefreshKnownGroupsAsync(TaskContext context, CancellationToken cancellationToken)
    {
        if (_groupMemberApi == null || Volatile.Read(ref _memberApiUnavailable) != 0)
        {
            return;
        }

        foreach (var groupOpenId in ChannelMapping.Keys)
        {
            if (cancellationToken.IsCancellationRequested || Volatile.Read(ref _memberApiUnavailable) != 0)
            {
                break;
            }

            await TryRefreshGroupMembersAsync(groupOpenId, cancellationToken);
        }
    }

    private async Task<ChannelInfo?> EnsureChannelInfoAsync(string channelId, string? subChannelId)
    {
        if (ChannelMapping.TryGetValue(channelId, out var existing))
        {
            return existing;
        }

        var result = await TryGetOrAddChannelInfo(channelId, subChannelId);
        return result.IsSuccess ? result.ChannelInfo : null;
    }

    private async Task<QGroupMember?> TryFetchGroupMemberFromApiAsync(string groupOpenId, string memberOpenId,
        CancellationToken cancellationToken = default)
    {
        if (_groupMemberApi == null || Volatile.Read(ref _memberApiUnavailable) != 0)
        {
            return null;
        }

        try
        {
            return await _groupMemberApi.GetGroupMemberAsync(groupOpenId, memberOpenId, cancellationToken);
        }
        catch (QApiException ex)
        {
            HandleMemberApiException(ex, groupOpenId, memberOpenId);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "获取 QQ 群 {GroupOpenId} 成员 {MemberOpenId} 信息失败。", groupOpenId, memberOpenId);
            return null;
        }
    }

    private void HandleMemberApiException(QApiException ex, string groupOpenId, string? memberOpenId = null)
    {
        if (ex.Code == MissingPermissionCode)
        {
            if (Interlocked.Exchange(ref _memberApiUnavailable, 1) == 0)
            {
                _logger.LogWarning(
                    "QQ 群成员管理接口返回错误码 11253（仅白名单机器人可用），已自动停用主动成员查询，后续将仅依赖群消息事件中的 author.member_role。");
            }

            return;
        }

        if (memberOpenId == null)
        {
            _logger.LogWarning("刷新 QQ 群 {GroupOpenId} 成员列表接口返回错误：Code={Code}, Message={Message}",
                groupOpenId, ex.Code, ex.Message);
        }
        else
        {
            _logger.LogDebug("获取 QQ 群 {GroupOpenId} 成员 {MemberOpenId} 信息接口返回错误：Code={Code}, Message={Message}",
                groupOpenId, memberOpenId, ex.Code, ex.Message);
        }
    }

    private async Task SyncMemberFromMessageAsync(MemberInfo memberInfo, string? username, string? memberNickname, MemberRole? memberRole)
    {
        var events = CollectMemberUpdates(memberInfo, username, memberNickname, memberRole);
        if (events.Count > 0)
        {
            await NotifyContactsUpdatedAsync(new ContactsUpdateEvent { Events = events });
        }
    }

    private List<ContactsUpdateSingleEvent> ReconcileGroupMembers(ChannelInfo channelInfo, IReadOnlyList<QGroupMember> remoteMembers)
    {
        var events = new List<ContactsUpdateSingleEvent>();
        var remoteById = new Dictionary<string, QGroupMember>(StringComparer.Ordinal);
        foreach (var remote in remoteMembers)
        {
            if (!string.IsNullOrWhiteSpace(remote.MemberOpenId))
            {
                remoteById[remote.MemberOpenId] = remote;
            }
        }

        foreach (var (memberId, remote) in remoteById)
        {
            if (channelInfo.Members.TryGetValue(memberId, out var existing))
            {
                events.AddRange(CollectMemberUpdates(existing, remote.Username, remote.MemberNickname, remote.Role));
            }
            else
            {
                var newMember = new MemberInfo(channelInfo.ChannelId, memberId, channelInfo.SubChannelId)
                {
                    Nickname = remote.Username,
                    Card = remote.MemberNickname,
                    MemberRole = remote.Role ?? MemberRole.Member
                };

                if (channelInfo.Members.TryAdd(memberId, newMember))
                {
                    events.Add(new ContactsUpdateSingleEvent
                    {
                        MemberInfo = newMember,
                        UpdateRole = ContactsUpdateRole.Member,
                        UpdateType = ContactsUpdateType.Added
                    });
                }
                else
                {
                    events.AddRange(CollectMemberUpdates(channelInfo.Members[memberId], remote.Username, remote.MemberNickname, remote.Role));
                }
            }
        }

        foreach (var cachedMemberId in channelInfo.Members.Keys)
        {
            if (remoteById.ContainsKey(cachedMemberId))
            {
                continue;
            }

            if (channelInfo.Members.TryRemove(cachedMemberId, out var removed))
            {
                events.Add(new ContactsUpdateSingleEvent
                {
                    MemberInfo = removed,
                    UpdateRole = ContactsUpdateRole.Member,
                    UpdateType = ContactsUpdateType.Removed
                });
            }
        }

        return events;
    }

    private List<ContactsUpdateSingleEvent> CollectMemberUpdates(MemberInfo memberInfo,
        string? username,
        string? memberNickname,
        MemberRole? memberRole)
    {
        var events = new List<ContactsUpdateSingleEvent>();

        lock (memberInfo)
        {
            if (!string.IsNullOrWhiteSpace(username) &&
                !string.Equals(memberInfo.Nickname, username, StringComparison.Ordinal))
            {
                memberInfo.Nickname = username;
                events.Add(new ContactsUpdateSingleEvent
                {
                    ChangedPath = nameof(MemberInfo.Nickname),
                    MemberInfo = memberInfo,
                    UpdateRole = ContactsUpdateRole.Member,
                    UpdateType = ContactsUpdateType.Changed
                });
            }

            if (!string.IsNullOrWhiteSpace(memberNickname) &&
                !string.Equals(memberInfo.Card, memberNickname, StringComparison.Ordinal))
            {
                memberInfo.Card = memberNickname;
                events.Add(new ContactsUpdateSingleEvent
                {
                    ChangedPath = nameof(MemberInfo.Card),
                    MemberInfo = memberInfo,
                    UpdateRole = ContactsUpdateRole.Member,
                    UpdateType = ContactsUpdateType.Changed
                });
            }

            if (memberRole.HasValue && memberInfo.MemberRole != memberRole.Value)
            {
                _logger.LogInformation("Changed channel {ChannelId} member {UserId} role from: {OldRole} to {NewRole}",
                    memberInfo.ChannelId, memberInfo.UserId, memberInfo.MemberRole, memberRole.Value);
                memberInfo.MemberRole = memberRole.Value;
                events.Add(new ContactsUpdateSingleEvent
                {
                    ChangedPath = nameof(MemberInfo.MemberRole),
                    MemberInfo = memberInfo,
                    UpdateRole = ContactsUpdateRole.Member,
                    UpdateType = ContactsUpdateType.Changed
                });
            }
        }

        return events;
    }

    private static string? TryGetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return prop.GetString();
    }
}
