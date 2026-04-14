using Discord.WebSocket;
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
}