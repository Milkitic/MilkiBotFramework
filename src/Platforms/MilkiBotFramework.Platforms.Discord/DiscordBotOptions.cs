using System.ComponentModel;
using Discord;

namespace MilkiBotFramework.Platforms.Discord;

public class DiscordBotOptions : BotOptions
{
    /// <summary>
    ///     Discord Gateway Intents 配置。
    ///     默认值为 <see cref="Discord.GatewayIntents.AllUnprivileged" /> | <see cref="Discord.GatewayIntents.MessageContent" />。
    ///     <para>如需启用特权 Intent（如 GuildMembers、Presences 等），需在此处设置。</para>
    /// </summary>
    public GatewayIntents? GatewayIntents { get; set; }

    [Description("Discord 代理配置。未配置 Url 时，会回退到 HttpOptions.ProxyUrl；UseSystemProxy=true 时可直接使用系统代理。")]
    public DiscordProxyOptions Proxy { get; set; } = new();

    public string Token { get; set; } = string.Empty;
}

public class DiscordProxyOptions
{
    [Description("是否启用 Discord 代理")]
    public bool Enabled { get; set; }

    [Description("是否使用系统代理；若同时配置 Url，则优先使用 Url")]
    public bool UseSystemProxy { get; set; }

    [Description("自定义代理地址，例如 http://127.0.0.1:7890")]
    public string? Url { get; set; }
}