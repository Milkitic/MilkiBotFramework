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

    public SocketMessage? SocketMessage { get; internal set; }

    public SocketSlashCommand? SlashCommand { get; internal set; }

    public DiscordContactEvent? ContactEvent { get; internal set; }

    public bool InteractionResponseSent { get; internal set; }
}

public abstract record DiscordContactEvent;

public sealed record DiscordChannelCreated(string GuildId, string ChannelId, string? Name) : DiscordContactEvent;

public sealed record DiscordChannelRemoved(string GuildId, string ChannelId) : DiscordContactEvent;

public sealed record DiscordMemberJoined(string GuildId, string UserId, string? Nickname, MemberRole MemberRole)
    : DiscordContactEvent;

public sealed record DiscordMemberLeft(string GuildId, string UserId) : DiscordContactEvent;

public sealed record DiscordMemberUpdated(string GuildId, string UserId, string? Nickname, MemberRole MemberRole)
    : DiscordContactEvent;