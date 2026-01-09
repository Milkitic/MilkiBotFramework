using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.Discord.Connecting;
using MilkiBotFramework.Platforms.Discord.Messaging;

namespace MilkiBotFramework.Platforms.Discord.Dispatching;

public class DiscordDispatcher : DispatcherBase<DiscordMessageContext>
{
    public DiscordDispatcher(IConnector connector, 
        IContactsManager contactsManager, 
        ILogger<DiscordDispatcher> logger, 
        IServiceProvider serviceProvider, 
        BotOptions botOptions, 
        EventBus eventBus) 
        : base(connector, contactsManager, logger, serviceProvider, botOptions, eventBus)
    {
    }

    protected override bool TryGetIdentityByRawMessage(DiscordMessageContext messageContext, 
        [NotNullWhen(true)] out MessageIdentity? messageIdentity, 
        out string? strIdentity)
    {
        var guid = messageContext.RawTextMessage;
        strIdentity = guid;
        
        if (guid != null && DiscordConnector.MessageCache.TryGetValue(guid, out var socketMessage))
        {
            messageContext.SocketMessage = socketMessage;
            
            // 确定消息类型
            var messageType = socketMessage.Channel is global::Discord.IDMChannel 
                ? MessageType.Private 
                : MessageType.Channel;

            var userId = socketMessage.Author.Id.ToString();
            
            if (messageType == MessageType.Channel)
            {
                var channelId = socketMessage.Channel.Id.ToString();
                var guildId = (socketMessage.Channel as global::Discord.IGuildChannel)?.GuildId.ToString();
                
                // 如果是 Guild Channel，我们可以把 GuildId 当作 GroupId 或者区分处理
                // 这里简单映射：MessageIdentity(GroupId, ChannelId, Type)
                // MilkiBotFramework 的 MessageIdentity 定义为 (GroupId, ChannelId, Type)
                // 对于 Discord，通常 GuildId -> GroupId, ChannelId -> ChannelId
                
                messageIdentity = new MessageIdentity(guildId, channelId, MessageType.Channel);
            }
            else
            {
                messageIdentity = new MessageIdentity(userId, MessageType.Private);
            }

            messageContext.MessageUserIdentity = new MessageUserIdentity(messageIdentity, userId);
            messageContext.ReceivedTime = socketMessage.Timestamp;
            messageContext.MessageId = socketMessage.Id.ToString();
            
            return true;
        }

        messageIdentity = null;
        return false;
    }

    protected override bool TrySetTextMessage(DiscordMessageContext messageContext)
    {
        if (messageContext.SocketMessage != null)
        {
            messageContext.TextMessage = messageContext.SocketMessage.Content;
            return true;
        }
        return false;
    }
}
