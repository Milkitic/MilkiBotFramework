using Discord.WebSocket;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Platforms.Discord.Messaging;

public class DiscordMessageContext : MessageContext
{
    public DiscordMessageContext(IRichMessageConverter richMessageConverter)
        : base(richMessageConverter)
    {
    }

    /// <summary>
    ///     Discord 原始消息对象，由 <see cref="Dispatching.DiscordDispatcher" /> 在分发时赋值。
    /// </summary>
    public SocketMessage? SocketMessage { get; internal set; }

    /// <summary>
    ///     Discord 联系人变更事件，由 <see cref="Dispatching.DiscordDispatcher" /> 在分发 Notice 时赋值。
    /// </summary>
    public DiscordContactEvent? ContactEvent { get; internal set; }
}

public abstract record DiscordContactEvent;

public sealed record DiscordChannelCreated(string GuildId, string ChannelId, string? Name) : DiscordContactEvent;

public sealed record DiscordChannelRemoved(string GuildId, string ChannelId) : DiscordContactEvent;

public sealed record DiscordMemberJoined(string GuildId, string UserId, string? Nickname, MemberRole MemberRole)
    : DiscordContactEvent;

public sealed record DiscordMemberLeft(string GuildId, string UserId) : DiscordContactEvent;

public sealed record DiscordMemberUpdated(string GuildId, string UserId, string? Nickname, MemberRole MemberRole)
    : DiscordContactEvent;