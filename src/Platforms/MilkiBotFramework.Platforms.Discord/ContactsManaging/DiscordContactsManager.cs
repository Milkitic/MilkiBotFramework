using System.Diagnostics.CodeAnalysis;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.Discord.Connecting;
using MilkiBotFramework.Platforms.Discord.Messaging;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Platforms.Discord.ContactsManaging;

public class DiscordContactsManager : ContactsManagerBase
{
    private readonly DiscordConnector _connector;
    private readonly ILogger<DiscordContactsManager> _logger;

    public DiscordContactsManager(DiscordConnector connector,
        BotTaskScheduler botTaskScheduler,
        ILogger<DiscordContactsManager> logger,
        EventBus eventBus) : base(botTaskScheduler, logger, eventBus)
    {
        _connector = connector;
        _logger = logger;
    }

    public override async Task<SelfInfoResult> TryGetOrUpdateSelfInfo()
    {
        var baseResult = await base.TryGetOrUpdateSelfInfo();
        if (baseResult.IsSuccess) return baseResult;

        var currentUser = _connector.Client.CurrentUser;
        if (currentUser == null)
            return SelfInfoResult.Fail;

        var selfInfo = new SelfInfo
        {
            UserId = currentUser.Id.ToString(),
            Nickname = currentUser.Username
        };
        SelfInfo = selfInfo;
        return new SelfInfoResult { IsSuccess = true, SelfInfo = selfInfo };
    }

    public override async Task<PrivateInfoResult> TryGetOrAddPrivateInfo(string userId)
    {
        var baseResult = await base.TryGetOrAddPrivateInfo(userId);
        if (baseResult.IsSuccess) return baseResult;

        if (!ulong.TryParse(userId, out var id))
            return PrivateInfoResult.Fail;

        var user = await _connector.Client.GetUserAsync(id);
        if (user == null)
            return PrivateInfoResult.Fail;

        var privateInfo = new PrivateInfo(userId)
        {
            Nickname = user.Username
        };
        PrivateMapping.AddOrUpdate(userId, privateInfo, (_, _) => privateInfo);
        return new PrivateInfoResult { IsSuccess = true, PrivateInfo = privateInfo };
    }

    /// <summary>
    /// 尝试获取或添加频道信息。
    /// <para>注意：在 Discord 适配器中，<paramref name="channelId"/> 参数实际传入的是 GuildId（由 Dispatcher 的 MessageIdentity.Id 映射），
    /// 而非 Discord 的 ChannelId。Discord 的 ChannelId 通过 <paramref name="subChannelId"/> 传入。</para>
    /// </summary>
    public override async Task<ChannelInfoResult> TryGetOrAddChannelInfo(string channelId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddChannelInfo(channelId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;

        if (!ulong.TryParse(channelId, out var guildId))
            return ChannelInfoResult.Fail;

        var guild = _connector.Client.GetGuild(guildId);
        if (guild == null)
            return ChannelInfoResult.Fail;

        if (subChannelId == null)
        {
            var channelInfo = new ChannelInfo(channelId)
            {
                Name = guild.Name
            };
            ChannelMapping.AddOrUpdate(channelId, channelInfo, (_, _) => channelInfo);
            return new ChannelInfoResult { IsSuccess = true, ChannelInfo = channelInfo };
        }

        if (!ulong.TryParse(subChannelId, out var subId))
            return ChannelInfoResult.Fail;

        var channel = await _connector.Client.GetChannelAsync(subId);
        if (channel is not SocketGuildChannel guildChannel)
            return ChannelInfoResult.Fail;

        // 只处理文本类频道，避免把语音等类型错误映射为可发送子频道
        if (channel is not SocketTextChannel and not SocketThreadChannel)
            return ChannelInfoResult.Fail;

        if (!ChannelMapping.ContainsKey(channelId))
        {
            var rootChannelInfo = new ChannelInfo(channelId)
            {
                Name = guild.Name
            };
            ChannelMapping.AddOrUpdate(channelId, rootChannelInfo, (_, _) => rootChannelInfo);
        }

        var subChannelInfo = new ChannelInfo(channelId)
        {
            SubChannelId = subChannelId,
            Name = guildChannel.Name
        };

        var subChannels = SubChannelMapping.GetOrAdd(channelId, _ => new());
        subChannels.AddOrUpdate(subChannelId, subChannelInfo, (_, _) => subChannelInfo);
        return new ChannelInfoResult { IsSuccess = true, ChannelInfo = subChannelInfo };
    }

    /// <summary>
    /// 尝试获取或添加成员信息。
    /// <para>注意：在 Discord 适配器中，<paramref name="channelId"/> 参数实际传入的是 GuildId（由 Dispatcher 的 MessageIdentity.Id 映射），
    /// 这是因为框架的 channelId 在群组场景下对应 Discord 的 GuildId。</para>
    /// </summary>
    public override async Task<MemberInfoResult> TryGetOrAddMemberInfo(string channelId, string userId, string? subChannelId = null)
    {
        var baseResult = await base.TryGetOrAddMemberInfo(channelId, userId, subChannelId);
        if (baseResult.IsSuccess) return baseResult;

        // channelId 在 Discord 场景下实际为 GuildId
        if (!ulong.TryParse(channelId, out var guildId) || !ulong.TryParse(userId, out var id))
            return MemberInfoResult.Fail;

        var guild = _connector.Client.GetGuild(guildId);
        if (guild == null)
            return MemberInfoResult.Fail;

        var guildUser = guild.GetUser(id);

        if (guildUser == null)
            return MemberInfoResult.Fail;

        var memberInfo = new MemberInfo(channelId, userId, subChannelId)
        {
            Nickname = guildUser.Nickname,
            MemberRole = GetMemberRole(guildUser)
        };

        var targetChannels = subChannelId == null
            ? ChannelMapping
            : SubChannelMapping.GetOrAdd(channelId, _ => new());

        var targetKey = subChannelId ?? channelId;
        var success = targetChannels.TryGetValue(targetKey, out var channelInfo);
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

    protected override bool GetContactsUpdateInfo(MessageContext messageContext, out ContactsUpdateInfo? updateInfo)
    {
        if (messageContext is not DiscordMessageContext { ContactEvent: not null } discordMessageContext)
        {
            updateInfo = null;
            return false;
        }

        updateInfo = discordMessageContext.ContactEvent switch
        {
            DiscordChannelCreated created => new ContactsUpdateInfo(created.GuildId)
            {
                SubId = created.ChannelId,
                Name = created.Name,
                ContactsUpdateRole = ContactsUpdateRole.SubChannel,
                ContactsUpdateType = ContactsUpdateType.Added
            },
            DiscordChannelRemoved removed => new ContactsUpdateInfo(removed.GuildId)
            {
                SubId = removed.ChannelId,
                ContactsUpdateRole = ContactsUpdateRole.SubChannel,
                ContactsUpdateType = ContactsUpdateType.Removed
            },
            DiscordMemberJoined joined => new ContactsUpdateInfo(joined.GuildId)
            {
                UserId = joined.UserId,
                Name = joined.Nickname,
                MemberRole = joined.MemberRole,
                ContactsUpdateRole = ContactsUpdateRole.Member,
                ContactsUpdateType = ContactsUpdateType.Added
            },
            DiscordMemberLeft left => new ContactsUpdateInfo(left.GuildId)
            {
                UserId = left.UserId,
                ContactsUpdateRole = ContactsUpdateRole.Member,
                ContactsUpdateType = ContactsUpdateType.Removed
            },
            DiscordMemberUpdated updated => new ContactsUpdateInfo(updated.GuildId)
            {
                UserId = updated.UserId,
                Name = updated.Nickname,
                MemberRole = updated.MemberRole,
                ContactsUpdateRole = ContactsUpdateRole.Member,
                ContactsUpdateType = ContactsUpdateType.Changed
            },
            _ => null
        };

        return updateInfo != null;
    }

    protected override bool GetContactsCore(
        [NotNullWhen(true)] out Dictionary<string, ChannelInfo>? channels,
        [NotNullWhen(true)] out Dictionary<string, ChannelInfo>? subChannels,
        [NotNullWhen(true)] out Dictionary<string, PrivateInfo>? privates)
    {
        channels = new Dictionary<string, ChannelInfo>();
        subChannels = new Dictionary<string, ChannelInfo>();
        privates = new Dictionary<string, PrivateInfo>();

        foreach (var guild in _connector.Client.Guilds)
        {
            var guildId = guild.Id.ToString();
            var rootChannelInfo = new ChannelInfo(guildId)
            {
                Name = guild.Name
            };

            foreach (var user in guild.Users)
            {
                if (user.IsBot)
                    continue;

                var memberInfo = new MemberInfo(guildId, user.Id.ToString(), null)
                {
                    Nickname = user.Nickname,
                    MemberRole = GetMemberRole(user)
                };

                rootChannelInfo.Members.TryAdd(memberInfo.UserId, memberInfo);
            }

            channels[guildId] = rootChannelInfo;

            var subChannelDict = SubChannelMapping.GetOrAdd(guildId, _ => new());
            foreach (var channel in guild.Channels)
            {
                if (channel is not SocketTextChannel and not SocketThreadChannel)
                    continue;

                var channelId = channel.Id.ToString();
                var subChannelInfo = new ChannelInfo(guildId)
                {
                    SubChannelId = channelId,
                    Name = channel.Name
                };

                foreach (var user in guild.Users)
                {
                    if (user.IsBot)
                        continue;

                    var memberInfo = new MemberInfo(guildId, user.Id.ToString(), channelId)
                    {
                        Nickname = user.Nickname,
                        MemberRole = GetMemberRole(user)
                    };

                    subChannelInfo.Members.TryAdd(memberInfo.UserId, memberInfo);
                }

                subChannelDict[channelId] = subChannelInfo;
                subChannels[channelId] = subChannelInfo;
            }
        }

        return true;
    }

    private static MemberRole GetMemberRole(SocketGuildUser user)
    {
        if (user.Guild.OwnerId == user.Id)
            return MemberRole.Owner;
        if (user.Roles.Any(r => r.Permissions.Administrator))
            return MemberRole.Admin;
        // 简单处理：有管理权限视为 Admin，其余为 Member
        return MemberRole.Member;
    }
}
