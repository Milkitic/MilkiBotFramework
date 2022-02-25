using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContractsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events;

namespace MilkiBotFramework.Platforms.GoCqHttp.Dispatching
{
    public class GoCqDispatcher : DispatcherBase<GoCqMessageContext>
    {
        private readonly GoCqApi _goCqApi;
        private readonly IRichMessageConverter _richMessageConverter;

        public GoCqDispatcher(GoCqApi goCqApi,
            IContractsManager contractsManager,
            IRichMessageConverter richMessageConverter,
            ILogger<GoCqDispatcher> logger)
            : base(goCqApi.Connector, contractsManager, logger)
        {
            _goCqApi = goCqApi;
            _richMessageConverter = richMessageConverter;
        }

        protected override bool TrySetTextMessage(GoCqMessageContext messageContext)
        {
            if (messageContext.MessageIdentity == MessageIdentity.MetaMessage ||
                messageContext.MessageIdentity == MessageIdentity.NoticeMessage) return false;
            messageContext.TextMessage = messageContext.RawMessage.Message;
            return true;
        }

        protected override bool TryGetIdentityByRawMessage(GoCqMessageContext messageContext,
            [NotNullWhen(true)] out MessageIdentity? messageIdentity,
            out string? strIdentity)
        {
            var rawJson = messageContext.RawTextMessage;
            strIdentity = null;

            var jDoc = JsonDocument.Parse(rawJson);
            var hasProperty = jDoc.RootElement.TryGetProperty("post_type", out var postTypeElement);
            if (!hasProperty)
            {
                messageIdentity = null;
                return false;
            }

            messageContext.RawJsonDocument = jDoc;
            var postType = postTypeElement.GetString();

            if (postType == "meta_event")
            {
                messageIdentity = MessageIdentity.MetaMessage;
                return true;
            }

            if (postType == "notice")
            {
                messageIdentity = MessageIdentity.NoticeMessage;
                return true;
            }

            if (postType == "message")
            {
                var messageType = jDoc.RootElement.GetProperty("message_type").GetString();
                if (messageType == "private")
                {
                    var parsedObj = JsonSerializer.Deserialize<PrivateMessage>(rawJson)!;
                    messageIdentity = new MessageIdentity(parsedObj.UserId, MessageType.Private);

                    messageContext.RawMessage = parsedObj;
                    messageContext.UserId = parsedObj.UserId;
                    messageContext.ReceivedTime = parsedObj.Time;
                    return true;
                }

                if (messageType == "group")
                {
                    var parsedObj = JsonSerializer.Deserialize<GroupMessage>(rawJson)!;
                    messageIdentity = new MessageIdentity(parsedObj.GroupId, MessageType.Channel);

                    messageContext.RawMessage = parsedObj;
                    messageContext.UserId = parsedObj.UserId;
                    messageContext.ReceivedTime = parsedObj.Time;
                    return true;
                }

                if (messageType == "guild")
                {
                    var parsedObj = JsonSerializer.Deserialize<GuildMessage>(rawJson)!;
                    messageIdentity = new MessageIdentity(parsedObj.GuildId, parsedObj.ChannelId, MessageType.Channel);

                    messageContext.RawMessage = parsedObj;
                    messageContext.UserId = parsedObj.UserId;
                    messageContext.ReceivedTime = parsedObj.Time;
                    return true;
                }

                messageIdentity = null;
                strIdentity = postType + "." + messageType;
                return false;
            }

            messageIdentity = null;
            strIdentity = postType;
            return false;
        }
    }
}
