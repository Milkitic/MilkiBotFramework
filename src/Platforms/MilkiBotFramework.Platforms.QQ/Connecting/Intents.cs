namespace MilkiBotFramework.Platforms.QQ.Connecting;

/// <summary>
/// <seealso href="https://bot.q.qq.com/wiki/develop/api-v2/dev-prepare/interface-framework/event-emit.html#websocket-%E6%96%B9%E5%BC%8F"/>
/// </summary>
[Flags]
public enum Intents
{
    Guilds = 1 << 0,
    GuildMembers = 1 << 1,
    GuildMessages = 1 << 9,
    GuildMessageReactions = 1 << 10,
    DirectMessage = 1 << 12,
    GroupAndC2CEvent = 1 << 25,
    Interaction = 1 << 26,
    MessageAudit = 1 << 27,
    ForumsEvent = 1 << 28,
    AudioAction = 1 << 29,
    PublicGuildMessages = 1 << 30,
}