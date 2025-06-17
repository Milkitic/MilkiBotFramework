using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.QQ.Messaging;

namespace MilkiBotFramework.Platforms.QQ.Dispatching;

public class QDispatcher : DispatcherBase<QMessageContext>
{
    public QDispatcher(IConnector connector,
        IContactsManager contactsManager,
        ILogger<QDispatcher> logger,
        IServiceProvider serviceProvider,
        BotOptions botOptions,
        EventBus eventBus)
        : base(connector, contactsManager, logger, serviceProvider, botOptions, eventBus)
    {
    }

    protected override bool TrySetTextMessage(QMessageContext messageContext)
    {
        if (messageContext.MessageIdentity == MessageIdentity.MetaMessage ||
            messageContext.MessageIdentity == MessageIdentity.NoticeMessage) return false;
        messageContext.TextMessage = messageContext.RawMessage;
        //throw new NotImplementedException();
        return true;
    }

    protected override bool TryGetIdentityByRawMessage(QMessageContext messageContext,
        [NotNullWhen(true)] out MessageIdentity? messageIdentity,
        out string? strIdentity)
    {
        var rawJson = messageContext.RawTextMessage;
        strIdentity = null;

        var jDoc = JsonDocument.Parse(rawJson);
        messageContext.RawJsonDocument = jDoc;

        var rootElement = jDoc.RootElement;
        if (rootElement.TryGetProperty("t", out var tProp))
        {
            if (tProp.GetString() == "GROUP_AT_MESSAGE_CREATE")
            {
                if (rootElement.TryGetProperty("d", out var dProp))
                {
                    var messageId = dProp.GetProperty("id").GetString()!;
                    var memberId = dProp.GetProperty("author").GetProperty("member_openid").GetString()!;
                    var groupId = dProp.GetProperty("group_id").GetString()!;
                    var content = dProp.GetProperty("content").GetString()!.Trim();
                    var timestamp = DateTimeOffset.Parse(dProp.GetProperty("timestamp").GetString()!);

                    messageIdentity = new MessageIdentity(groupId, MessageType.Channel);

                    messageContext.RawMessage = content;
                    messageContext.MessageUserIdentity = new MessageUserIdentity(messageIdentity, memberId);
                    messageContext.ReceivedTime = timestamp;
                    messageContext.MessageId = messageId;
                    return true;
                }
            }
            else if (tProp.GetString() == "C2C_MESSAGE_CREATE")
            {
                if (rootElement.TryGetProperty("d", out var dProp))
                {
                    var messageId = dProp.GetProperty("id").GetString()!;
                    var userId = dProp.GetProperty("author").GetProperty("user_openid").GetString()!;
                    var content = dProp.GetProperty("content").GetString()!.Trim();
                    var timestamp = DateTimeOffset.Parse(dProp.GetProperty("timestamp").GetString()!);

                    messageIdentity = new MessageIdentity(userId, MessageType.Private);

                    messageContext.RawMessage = content;
                    messageContext.MessageUserIdentity = new MessageUserIdentity(messageIdentity, userId);
                    messageContext.ReceivedTime = timestamp;
                    messageContext.MessageId = messageId;
                    return true;
                }
            }
            //throw new NotImplementedException();
        }

        messageIdentity = null;
        return false;
    }
}