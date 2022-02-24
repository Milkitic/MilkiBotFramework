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
        private readonly ILogger<MessageResponseContext> _logger2;

        public GoCqDispatcher(GoCqApi goCqApi,
            IContractsManager contractsManager,
            IRichMessageConverter richMessageConverter,
            ILogger<GoCqDispatcher> logger,
            ILogger<MessageResponseContext> logger2)
            : base(goCqApi.Connector, contractsManager, logger)
        {
            _goCqApi = goCqApi;
            _richMessageConverter = richMessageConverter;
            _logger2 = logger2;
        }

        protected override GoCqMessageContext CreateMessageContext(string rawMessage)
        {
            var goCqMessageContext = new GoCqMessageContext
            {
                Request = new GoCqMessageRequestContext(rawMessage, _richMessageConverter),
            };
            goCqMessageContext.Response =
                new MessageResponseContext(goCqMessageContext, _goCqApi, _logger2, _richMessageConverter);
            return goCqMessageContext;
        }

        protected override bool TrySetTextMessage(GoCqMessageContext messageContext)
        {
            if (messageContext.Request.Identity == MessageIdentity.MetaMessage ||
                messageContext.Request.Identity == MessageIdentity.NoticeMessage) return false;
            messageContext.GoCqRequest.TextMessage = messageContext.GoCqRequest.RawMessage.Message;
            return true;
        }

        protected override bool TryGetIdentityByRawMessage(GoCqMessageContext messageContext,
            [NotNullWhen(true)] out MessageIdentity? messageIdentity,
            out string? strIdentity)
        {
            var rawJson = messageContext.Request.RawTextMessage;
            strIdentity = null;

            var jDoc = JsonDocument.Parse(rawJson);
            var hasProperty = jDoc.RootElement.TryGetProperty("post_type", out var postTypeElement);
            if (!hasProperty)
            {
                messageIdentity = null;
                return false;
            }

            messageContext.GoCqRequest.RawJsonDocument = jDoc;
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
                    messageContext.GoCqRequest.RawMessage = parsedObj;
                    messageContext.GoCqRequest.UserId = parsedObj.UserId;
                    return true;
                }

                if (messageType == "group")
                {
                    var parsedObj = JsonSerializer.Deserialize<GroupMessage>(rawJson)!;
                    messageIdentity = new MessageIdentity(parsedObj.GroupId, MessageType.Public);
                    messageContext.GoCqRequest.RawMessage = parsedObj;
                    messageContext.GoCqRequest.UserId = parsedObj.UserId;
                    return true;
                }

                if (messageType == "guild")
                {
                    var parsedObj = JsonSerializer.Deserialize<GuildMessage>(rawJson)!;
                    messageIdentity = new MessageIdentity(parsedObj.GuildId, parsedObj.ChannelId, MessageType.Public);
                    messageContext.GoCqRequest.RawMessage = parsedObj;
                    messageContext.GoCqRequest.UserId = parsedObj.UserId;
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
