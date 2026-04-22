using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using MilkiBotFramework;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Platforms.Discord;
using MilkiBotFramework.Platforms.Discord.Messaging;
using MessageType = MilkiBotFramework.Messaging.MessageType;

namespace MilkiBotFramework.Platforms.Discord.Dispatching;

public class DiscordDispatcher : DispatcherBase<DiscordMessageContext>
{
    private readonly PluginCatalog _pluginCatalog;
    private readonly BotOptions _botOptions;

    public override string PlatformId => PlatformIds.Discord;

    public DiscordDispatcher(IMessageContextEnricher messageContextEnricher,
        MessageDispatchCoordinator messageDispatchCoordinator,
        ILogger<DiscordDispatcher> logger,
        IServiceProvider serviceProvider,
        PluginCatalog pluginCatalog,
        BotOptions botOptions)
        : base(messageContextEnricher, messageDispatchCoordinator, logger, serviceProvider)
    {
        _pluginCatalog = pluginCatalog;
        _botOptions = botOptions;
    }

    public override bool CanDispatch(InboundMessage inboundMessage)
    {
        return string.Equals(inboundMessage.Transport, PlatformId, StringComparison.OrdinalIgnoreCase)
               || inboundMessage.GetPayload<SocketMessage>() != null
               || inboundMessage.GetPayload<SocketSlashCommand>() != null
               || inboundMessage.GetPayload<DiscordContactEvent>() != null;
    }

    protected override bool TryPopulateMessageContext(DiscordMessageContext messageContext,
        InboundMessage inboundMessage,
        out string? failureReason)
    {
        failureReason = null;

        if (inboundMessage.GetPayload<DiscordContactEvent>() is { } contactEvent)
        {
            messageContext.ContactEvent = contactEvent;
            messageContext.MessageIdentity = MessageIdentity.NoticeMessage;
            messageContext.TextMessage = string.Empty;

            string? guildId = null;
            string? userId = null;
            switch (contactEvent)
            {
                case DiscordChannelCreated channelCreated:
                    guildId = channelCreated.GuildId;
                    break;
                case DiscordChannelRemoved channelRemoved:
                    guildId = channelRemoved.GuildId;
                    break;
                case DiscordMemberJoined memberJoined:
                    guildId = memberJoined.GuildId;
                    userId = memberJoined.UserId;
                    break;
                case DiscordMemberLeft memberLeft:
                    guildId = memberLeft.GuildId;
                    userId = memberLeft.UserId;
                    break;
                case DiscordMemberUpdated memberUpdated:
                    guildId = memberUpdated.GuildId;
                    userId = memberUpdated.UserId;
                    break;
            }

            if (guildId != null && userId != null)
            {
                var noticeIdentity = new MessageIdentity(guildId, MessageType.Notice);
                messageContext.MessageUserIdentity = new MessageUserIdentity(noticeIdentity, userId);
            }

            return true;
        }

        if (inboundMessage.GetPayload<SocketSlashCommand>() is { } slashCommand)
        {
            messageContext.SlashCommand = slashCommand;

            var messageType = slashCommand.Channel is IDMChannel
                ? MessageType.Private
                : MessageType.Channel;
            var userId = slashCommand.User.Id.ToString();

            if (messageType == MessageType.Channel)
            {
                var channelId = slashCommand.Channel.Id.ToString();
                var guildId = slashCommand.GuildId?.ToString() ?? channelId;
                messageContext.MessageIdentity = new MessageIdentity(guildId, channelId, MessageType.Channel);
            }
            else
            {
                messageContext.MessageIdentity = new MessageIdentity(userId, MessageType.Private);
            }

            messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity, userId);
            messageContext.ReceivedTime = slashCommand.CreatedAt;
            messageContext.MessageId = slashCommand.Id.ToString();

            var commandLineResult = DiscordSlashCommandHelper.BuildCommandLineResult(slashCommand, _pluginCatalog);
            messageContext.CommandLineResult = commandLineResult;
            messageContext.TextMessage = DiscordSlashCommandHelper.BuildDisplayText(commandLineResult, _botOptions.CommandFlag);
            return true;
        }

        if (inboundMessage.GetPayload<SocketMessage>() is { } socketMessage)
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
                messageContext.MessageIdentity = new MessageIdentity(guildId, channelId, MessageType.Channel);
            }
            else
            {
                messageContext.MessageIdentity = new MessageIdentity(userId, MessageType.Private);
            }

            messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity, userId);
            messageContext.ReceivedTime = socketMessage.Timestamp;
            messageContext.MessageId = socketMessage.Id.ToString();
            messageContext.TextMessage = socketMessage.Content;

            return true;
        }

        failureReason = inboundMessage.Payload?.GetType().Name ?? inboundMessage.RawText ?? "unknown-discord-inbound";
        return false;
    }
}