using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.OneBot.Messaging;
using MilkiBotFramework.Platforms.OneBot.Messaging.Events;

namespace MilkiBotFramework.Platforms.OneBot.Dispatching
{
    public class OneBotDispatcher : DispatcherBase<OneBotMessageContext>
    {
        public override string PlatformId => PlatformIds.OneBot;

        public OneBotDispatcher(IMessageContextEnricher messageContextEnricher,
            MessageDispatchCoordinator messageDispatchCoordinator,
            ILogger<OneBotDispatcher> logger,
            IServiceProvider serviceProvider)
            : base(messageContextEnricher, messageDispatchCoordinator, logger, serviceProvider)
        {
        }

        public override bool CanDispatch(InboundMessage inboundMessage)
        {
            if (string.Equals(inboundMessage.Transport, PlatformId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var rawJson = inboundMessage.RawText;
            return !string.IsNullOrWhiteSpace(rawJson) && rawJson.Contains("\"post_type\"", StringComparison.Ordinal);
        }

        protected override bool TryPopulateMessageContext(OneBotMessageContext messageContext,
            InboundMessage inboundMessage,
            out string? failureReason)
        {
            var rawJson = inboundMessage.RawText;
            failureReason = null;
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                failureReason = "empty-raw-text";
                return false;
            }

            JsonDocument jDoc;
            try
            {
                jDoc = JsonDocument.Parse(rawJson);
            }
            catch (JsonException)
            {
                failureReason = "invalid-json";
                return false;
            }

            var hasProperty = jDoc.RootElement.TryGetProperty("post_type", out var postTypeElement);
            if (!hasProperty)
            {
                failureReason = "missing-post_type";
                return false;
            }

            messageContext.RawJsonDocument = jDoc;
            if (jDoc.RootElement.TryGetProperty("self_id", out var selfIdElement))
            {
                messageContext.SelfId = selfIdElement.ValueKind switch
                {
                    JsonValueKind.String => selfIdElement.GetString(),
                    JsonValueKind.Number => selfIdElement.GetInt64().ToString(),
                    _ => null
                };
            }

            var postType = postTypeElement.GetString();

            if (postType == "meta_event")
            {
                messageContext.MessageIdentity = MessageIdentity.MetaMessage;
                return true;
            }

            if (postType == "notice")
            {
                messageContext.MessageIdentity = MessageIdentity.NoticeMessage;
                messageContext.TextMessage = string.Empty;
                return true;
            }

            if (postType == "message")
            {
                var messageType = jDoc.RootElement.GetProperty("message_type").GetString();
                if (messageType == "private")
                {
                    var parsedObj = JsonSerializer.Deserialize<PrivateMessage>(rawJson)!;
                    messageContext.MessageIdentity = new MessageIdentity(parsedObj.UserId, MessageType.Private);

                    messageContext.RawMessage = parsedObj;
                    messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity, parsedObj.UserId);
                    messageContext.ReceivedTime = parsedObj.Time;
                    messageContext.MessageId = parsedObj.MessageId;
                    messageContext.TextMessage = parsedObj.Message;
                    return true;
                }

                if (messageType == "group")
                {
                    var parsedObj = JsonSerializer.Deserialize<GroupMessage>(rawJson)!;
                    messageContext.MessageIdentity = new MessageIdentity(parsedObj.GroupId, MessageType.Channel);

                    messageContext.RawMessage = parsedObj;
                    messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity, parsedObj.UserId);
                    messageContext.ReceivedTime = parsedObj.Time;
                    messageContext.MessageId = parsedObj.MessageId;
                    messageContext.TextMessage = parsedObj.Message;
                    return true;
                }

                if (messageType == "guild")
                {
                    var parsedObj = JsonSerializer.Deserialize<GuildMessage>(rawJson)!;
                    messageContext.MessageIdentity = new MessageIdentity(parsedObj.GuildId, parsedObj.ChannelId, MessageType.Channel);

                    messageContext.RawMessage = parsedObj;
                    messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity, parsedObj.UserId);
                    messageContext.ReceivedTime = parsedObj.Time;
                    messageContext.MessageId = parsedObj.MessageId;
                    messageContext.TextMessage = parsedObj.Message;
                    return true;
                }

                failureReason = postType + "." + messageType;
                return false;
            }

            failureReason = postType;
            return false;
        }
    }
}
