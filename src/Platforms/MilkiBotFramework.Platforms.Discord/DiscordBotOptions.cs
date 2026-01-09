using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Platforms.Discord;

public class DiscordBotOptions : BotOptions
{
    public string Token { get; set; } = string.Empty;
}
