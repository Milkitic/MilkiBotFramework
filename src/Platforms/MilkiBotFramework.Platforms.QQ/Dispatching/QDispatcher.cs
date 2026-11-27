using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.QQ.Connecting.Models;
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
                   || rawJson.Contains("GROUP_MESSAGE_CREATE", StringComparison.Ordinal)
                   || rawJson.Contains("C2C_MESSAGE_CREATE", StringComparison.Ordinal)
                   || rawJson.Contains("GROUP_ADD_ROBOT", StringComparison.Ordinal)
                   || rawJson.Contains("GROUP_DEL_ROBOT", StringComparison.Ordinal)
                   || rawJson.Contains("FRIEND_ADD", StringComparison.Ordinal)
                   || rawJson.Contains("FRIEND_DEL", StringComparison.Ordinal));
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

        messageContext.RawJsonDocument = jDoc;

        var rootElement = jDoc.RootElement;
        if (rootElement.TryGetProperty("t", out var tProp) && tProp.ValueKind == JsonValueKind.String)
        {
            var eventType = tProp.GetString();
            messageContext.EventType = eventType;

            if (rootElement.TryGetProperty("d", out var dProp) && dProp.ValueKind == JsonValueKind.Object)
            {
                switch (eventType)
                {
                    case "GROUP_AT_MESSAGE_CREATE":
                    case "GROUP_MESSAGE_CREATE":
                        return TryPopulateGroupMessageContext(messageContext, dProp, out failureReason);
                    case "C2C_MESSAGE_CREATE":
                        return TryPopulateC2CMessageContext(messageContext, dProp, out failureReason);
                    case "GROUP_ADD_ROBOT":
                    case "GROUP_DEL_ROBOT":
                    case "FRIEND_ADD":
                    case "FRIEND_DEL":
                        messageContext.MessageIdentity = MessageIdentity.NoticeMessage;
                        messageContext.MessageId = TryGetOptionalString(rootElement, "id");
                        messageContext.RawMessage = string.Empty;
                        messageContext.TextMessage = string.Empty;
                        messageContext.ReceivedTime = ParseTimestampOrDefault(dProp, DateTimeOffset.Now);
                        return true;
                }
            }
        }

        failureReason = rootElement.TryGetProperty("t", out var identityProp)
            ? identityProp.GetString()
            : "unknown-qq-event";
        return false;
    }

    private static bool TryPopulateGroupMessageContext(QMessageContext messageContext,
        JsonElement dProp,
        out string? failureReason)
    {
        failureReason = null;

        var messageId = TryGetOptionalString(dProp, "id");
        if (string.IsNullOrWhiteSpace(messageId))
        {
            failureReason = "missing-message-id";
            return false;
        }

        if (!dProp.TryGetProperty("author", out var authorProp) || authorProp.ValueKind != JsonValueKind.Object)
        {
            failureReason = "missing-author";
            return false;
        }

        var memberId = TryGetOptionalString(authorProp, "member_openid");
        if (string.IsNullOrWhiteSpace(memberId))
        {
            failureReason = "missing-member-openid";
            return false;
        }

        var groupId = TryGetOptionalString(dProp, "group_openid");
        if (string.IsNullOrWhiteSpace(groupId))
        {
            groupId = TryGetOptionalString(dProp, "group_id");
        }

        if (string.IsNullOrWhiteSpace(groupId))
        {
            failureReason = "missing-group-openid";
            return false;
        }

        var content = (TryGetOptionalString(dProp, "content") ?? string.Empty).Trim();
        var timestamp = ParseTimestampOrDefault(dProp, DateTimeOffset.Now);

        messageContext.AuthorMemberRole = QMemberRoleParser.TryParse(TryGetOptionalString(authorProp, "member_role"), out var memberRole)
            ? memberRole
            : null;

        messageContext.AuthorUsername = TryGetOptionalString(authorProp, "username");
        messageContext.AuthorMemberNickname = TryGetOptionalString(authorProp, "member_nickname");

        messageContext.MessageIdentity = new MessageIdentity(groupId, MessageType.Channel);
        messageContext.RawMessage = content;
        messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity, memberId);
        messageContext.ReceivedTime = timestamp;
        messageContext.MessageId = messageId;
        messageContext.TextMessage = content;
        return true;
    }

    private static bool TryPopulateC2CMessageContext(QMessageContext messageContext,
        JsonElement dProp,
        out string? failureReason)
    {
        failureReason = null;

        var messageId = TryGetOptionalString(dProp, "id");
        if (string.IsNullOrWhiteSpace(messageId))
        {
            failureReason = "missing-message-id";
            return false;
        }

        if (!dProp.TryGetProperty("author", out var authorProp) || authorProp.ValueKind != JsonValueKind.Object)
        {
            failureReason = "missing-author";
            return false;
        }

        var userId = TryGetOptionalString(authorProp, "user_openid");
        if (string.IsNullOrWhiteSpace(userId))
        {
            failureReason = "missing-user-openid";
            return false;
        }

        var content = (TryGetOptionalString(dProp, "content") ?? string.Empty).Trim();
        var timestamp = ParseTimestampOrDefault(dProp, DateTimeOffset.Now);

        messageContext.AuthorUsername = TryGetOptionalString(authorProp, "username");
        messageContext.MessageIdentity = new MessageIdentity(userId, MessageType.Private);
        messageContext.RawMessage = content;
        messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity, userId);
        messageContext.ReceivedTime = timestamp;
        messageContext.MessageId = messageId;
        messageContext.TextMessage = content;
        return true;
    }

    private static string? TryGetOptionalString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return prop.GetString();
    }

    private static DateTimeOffset ParseTimestampOrDefault(JsonElement element, DateTimeOffset fallback)
    {
        if (!element.TryGetProperty("timestamp", out var tsProp))
        {
            return fallback;
        }

        if (tsProp.ValueKind == JsonValueKind.Number && tsProp.TryGetInt64(out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        if (tsProp.ValueKind == JsonValueKind.String)
        {
            var raw = tsProp.GetString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedUnix))
                {
                    return DateTimeOffset.FromUnixTimeSeconds(parsedUnix);
                }

                if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDate))
                {
                    return parsedDate;
                }
            }
        }

        return fallback;
    }
}
