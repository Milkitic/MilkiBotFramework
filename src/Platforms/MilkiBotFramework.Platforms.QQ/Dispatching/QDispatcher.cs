using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.QQ.Messaging;

namespace MilkiBotFramework.Platforms.QQ.Dispatching;

public class QDispatcher : DispatcherBase<QMessageContext>
{
    public override string PlatformId => PlatformIds.Qq;

    public QDispatcher(IMessageContextEnricher messageContextEnricher,
        MessageDispatchCoordinator messageDispatchCoordinator,
        ILogger<QDispatcher> logger,
        IServiceProvider serviceProvider)
        : base(messageContextEnricher, messageDispatchCoordinator, logger, serviceProvider)
    {
    }

    public override bool CanDispatch(InboundMessage inboundMessage)
    {
        var transport = inboundMessage.Transport;
        if (!string.IsNullOrWhiteSpace(transport) && transport.StartsWith("qq", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rawJson = inboundMessage.RawText;
        return !string.IsNullOrWhiteSpace(rawJson)
               && (rawJson.Contains("GROUP_AT_MESSAGE_CREATE", StringComparison.Ordinal)
                   || rawJson.Contains("C2C_MESSAGE_CREATE", StringComparison.Ordinal));
    }

    protected override bool TryPopulateMessageContext(QMessageContext messageContext,
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

                    messageContext.MessageIdentity = new MessageIdentity(groupId, MessageType.Channel);

                    messageContext.RawMessage = content;
                    messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity, memberId);
                    messageContext.ReceivedTime = timestamp;
                    messageContext.MessageId = messageId;
                    messageContext.TextMessage = content;
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

                    messageContext.MessageIdentity = new MessageIdentity(userId, MessageType.Private);

                    messageContext.RawMessage = content;
                    messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity, userId);
                    messageContext.ReceivedTime = timestamp;
                    messageContext.MessageId = messageId;
                    messageContext.TextMessage = content;
                    return true;
                }
            }
        }

        failureReason = jDoc.RootElement.TryGetProperty("t", out var identityProp)
            ? identityProp.GetString()
            : "unknown-qq-event";
        return false;
    }
}