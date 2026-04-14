using Discord;

namespace MilkiBotFramework.Platforms.Discord;

public class DiscordBotOptions : BotOptions
{
    /// <summary>
    ///     Discord Gateway Intents 配置。
    ///     默认值为 <see cref="Discord.GatewayIntents.AllUnprivileged" /> | <see cref="GatewayIntents.MessageContent" />。
    ///     <para>如需启用特权 Intent（如 GuildMembers、Presences 等），需在此处设置。</para>
    /// </summary>
    public GatewayIntents? GatewayIntents { get; set; }

    public string Token { get; set; } = string.Empty;
}