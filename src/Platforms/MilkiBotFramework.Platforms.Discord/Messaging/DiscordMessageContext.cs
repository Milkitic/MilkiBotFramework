using Discord.WebSocket;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Platforms.Discord.Messaging;

public class DiscordMessageContext : MessageContext
{
    public DiscordMessageContext(IRichMessageConverter richMessageConverter, SocketMessage? socketMessage) 
        : base(richMessageConverter)
    {
        SocketMessage = socketMessage;
    }

    public SocketMessage? SocketMessage { get; set; }
}
