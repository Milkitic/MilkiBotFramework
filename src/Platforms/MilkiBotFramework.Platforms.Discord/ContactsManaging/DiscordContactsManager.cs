using System.Diagnostics.CodeAnalysis;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.ContactsManaging.Results;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.Discord.Connecting;
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

        if (!ulong.TryParse(channelId, out var id))
            return ChannelInfoResult.Fail;

        var channel = await _connector.Client.GetChannelAsync(id);
        if (channel is not SocketGuildChannel guildChannel)
            return ChannelInfoResult.Fail;

        var channelInfo = new ChannelInfo(channelId)
        {
            Name = guildChannel.Name
        };
        ChannelMapping.AddOrUpdate(channelId, channelInfo, (_, _) => channelInfo);
        return new ChannelInfoResult { IsSuccess = true, ChannelInfo = channelInfo };
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

    protected override bool GetContactsUpdateInfo(MessageContext messageContext, out ContactsUpdateInfo? updateInfo)
    {
        updateInfo = null;
        return false;
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
            foreach (var channel in guild.Channels)
            {
                if (channel is not SocketTextChannel textChannel)
                    continue;

                var channelInfo = new ChannelInfo(channel.Id.ToString())
                {
                    Name = channel.Name
                };

                foreach (var user in guild.Users)
                {
                    if (user.IsBot)
                        continue;

                    var memberInfo = new MemberInfo(channel.Id.ToString(), user.Id.ToString(), null)
                    {
                        Nickname = user.Nickname,
                        MemberRole = GetMemberRole(user)
                    };
                    channelInfo.Members.TryAdd(memberInfo.UserId, memberInfo);
                }

                channels.TryAdd(channelInfo.ChannelId, channelInfo);
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
