using Discord;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.Discord.Messaging;

namespace MilkiBotFramework.Platforms.Discord.Connecting;

public class DiscordMessageApi : IMessageApi
{
    private readonly DiscordConnector _connector;

    public DiscordMessageApi(IConnector connector)
    {
        if (connector is DiscordConnector discordConnector)
        {
            _connector = discordConnector;
        }
        else
        {
            throw new ArgumentException("Connector must be DiscordConnector");
        }
    }

    public bool Supports(MessageContext messageContext)
    {
        return messageContext is DiscordMessageContext;
    }

    public async Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, MessageContext messageContext, string? subChannelId)
    {
        if (ulong.TryParse(channelId, out var id))
        {
            var channel = await _connector.Client.GetChannelAsync(id) as IMessageChannel;
            if (channel != null)
            {
                // 简单的文本发送，暂不处理 RichMessage 的复杂情况（如图片）
                // 实际需要实现 RichMessageConverter 来转换图片等
                var msg = await channel.SendMessageAsync(message);
                return msg.Id.ToString();
            }
        }
        return string.Empty;
    }

    public async Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage, MessageContext messageContext)
    {
        if (ulong.TryParse(userId, out var id))
        {
            var user = await _connector.Client.GetUserAsync(id);
            if (user != null)
            {
                var msg = await user.SendMessageAsync(message);
                return msg.Id.ToString();
            }
        }
        return string.Empty;
    }
}
