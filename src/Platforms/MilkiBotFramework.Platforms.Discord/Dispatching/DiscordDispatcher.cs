using System.Diagnostics.CodeAnalysis;
using Discord;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.Discord.Connecting;
using MilkiBotFramework.Platforms.Discord.Messaging;
using MessageType = MilkiBotFramework.Messaging.MessageType;

namespace MilkiBotFramework.Platforms.Discord.Dispatching;

public class DiscordDispatcher : DispatcherBase<DiscordMessageContext>
{
    private readonly DiscordConnector _discordConnector;

    public DiscordDispatcher(IConnector connector,
        IContactsManager contactsManager,
        ILogger<DiscordDispatcher> logger,
        IServiceProvider serviceProvider,
        BotOptions botOptions,
        EventBus eventBus)
        : base(connector, contactsManager, logger, serviceProvider, botOptions, eventBus)
    {
        _discordConnector = connector as DiscordConnector
                            ?? throw new ArgumentException("Connector must be DiscordConnector", nameof(connector));
    }

    protected override bool TryGetIdentityByRawMessage(DiscordMessageContext messageContext,
        [NotNullWhen(true)] out MessageIdentity? messageIdentity,
        out string? strIdentity)
    {
        var guid = messageContext.RawTextMessage;
        strIdentity = guid;

        if (guid != null && _discordConnector.MessageCache.TryGetValue(guid, out var socketMessage))
        {
            messageContext.SocketMessage = socketMessage;

            // 确定消息类型
            var messageType = socketMessage.Channel is IDMChannel
                ? MessageType.Private
                : MessageType.Channel;

            var userId = socketMessage.Author.Id.ToString();

            if (messageType == MessageType.Channel)
            {
                var channelId = socketMessage.Channel.Id.ToString();
                var guildId = (socketMessage.Channel as IGuildChannel)?.GuildId.ToString() ?? channelId;

                // Discord 频道映射：GuildId -> MessageIdentity.Id (群组标识), ChannelId -> MessageIdentity.SubId (子频道标识)
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