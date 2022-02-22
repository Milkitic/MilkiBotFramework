using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContractsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.GoCqHttp.Connecting;
using MilkiBotFramework.GoCqHttp.Messaging.Events;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.GoCqHttp.Dispatching
{
    public class GoCqDispatcher : DispatcherBase<GoCqMessageContext>
    {
        private readonly GoCqApi _goCqApi;

        public GoCqDispatcher(GoCqApi goCqApi, IContractsManager contractsManager, ILogger<GoCqDispatcher> logger)
            : base(goCqApi.Connector, contractsManager, logger)
        {
            _goCqApi = goCqApi;
        }

        protected override GoCqMessageContext CreateMessageContext(string rawMessage)
        {
            return new GoCqMessageContext(rawMessage);
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
                    return true;
                }

                if (messageType == "group")
                {
                    var parsedObj = JsonSerializer.Deserialize<GroupMessage>(rawJson)!;
                    messageIdentity = new MessageIdentity(parsedObj.GroupId, MessageType.Public);
                    messageContext.RawMessage = parsedObj;
                    messageContext.UserId = parsedObj.UserId;
                    return true;
                }

                if (messageType == "guild")
                {
                    var parsedObj = JsonSerializer.Deserialize<GuildMessage>(rawJson)!;
                    messageIdentity = new MessageIdentity(parsedObj.GuildId, parsedObj.ChannelId, MessageType.Public);
                    messageContext.RawMessage = parsedObj;
                    messageContext.UserId = parsedObj.UserId;
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

    public record GoCqMessageContext : MessageContext
    {
        public GoCqMessageContext(string rawTextMessage) : base(rawTextMessage)
        {
        }

        public JsonDocument RawJsonDocument { get; internal set; }
        public MessageBase RawMessage { get; internal set; }
    }
}
