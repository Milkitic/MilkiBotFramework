using Discord.WebSocket;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Platforms.Discord.Messaging;

public class DiscordMessageContext : MessageContext
{
    public SocketMessage? SocketMessage { get; set; }
}
