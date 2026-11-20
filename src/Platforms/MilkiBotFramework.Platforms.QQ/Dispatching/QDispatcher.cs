using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.QQ.Messaging;

namespace MilkiBotFramework.Platforms.QQ.Dispatching;

/// <summary>将 QQ 官方回调载荷转换成框架的群聊、单聊或通知上下文。</summary>
public class QDispatcher : DispatcherBase<QMessageContext>
{
    /// <summary>框架中 QQ 官方平台的标识。</summary>
    public override string PlatformId => PlatformIds.Qq;

    /// <summary>创建 QQ 事件分发器。</summary>
    /// <param name="messageContextEnricher">为消息补充联系人等信息的服务。</param>
    /// <param name="messageDispatchCoordinator">调度插件和消息通知的协调器。</param>
    /// <param name="logger">用于记录分发状态的日志对象。</param>
    /// <param name="serviceProvider">用于创建消息作用域的服务容器。</param>
    public QDispatcher(IMessageContextEnricher messageContextEnricher,
        MessageDispatchCoordinator messageDispatchCoordinator,
        ILogger<QDispatcher> logger,
        IServiceProvider serviceProvider)
        : base(messageContextEnricher, messageDispatchCoordinator, logger, serviceProvider)
    {
    }

    /// <summary>判断入站数据是否由 QQ 官方适配器处理。</summary>
    /// <param name="inboundMessage">带有传输标识或原始 JSON 的入站消息。</param>
    /// <returns>识别为 QQ 数据时返回 <see langword="true"/>。</returns>
    public override bool CanDispatch(InboundMessage inboundMessage)
    {
        var transport = inboundMessage.Transport;
        if (transport?.StartsWith("qq", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        return inboundMessage.RawText is { } rawJson &&
               (rawJson.Contains("GROUP_AT_MESSAGE_CREATE", StringComparison.Ordinal)
                || rawJson.Contains("GROUP_MESSAGE_CREATE", StringComparison.Ordinal)
                || rawJson.Contains("C2C_MESSAGE_CREATE", StringComparison.Ordinal));
    }

    /// <summary>从官方事件的外层载荷和数据对象填充消息上下文。</summary>
    /// <param name="messageContext">待填充的 QQ 上下文。</param>
    /// <param name="inboundMessage">要解析的入站数据。</param>
    /// <param name="failureReason">载荷无效时写入用于排查的原因。</param>
    /// <returns>事件已写入上下文时返回 <see langword="true"/>。</returns>
    protected override bool TryPopulateMessageContext(QMessageContext messageContext,
        InboundMessage inboundMessage, out string? failureReason)
    {
        failureReason = null;
        if (string.IsNullOrWhiteSpace(inboundMessage.RawText))
        {
            failureReason = "empty-raw-text";
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(inboundMessage.RawText);
        }
        catch (JsonException)
        {
            failureReason = "invalid-json";
            return false;
        }

        var root = document.RootElement;
        var eventType = GetString(root, "t");
        if (string.IsNullOrWhiteSpace(eventType) || !root.TryGetProperty("d", out var data)
            || data.ValueKind != JsonValueKind.Object)
        {
            document.Dispose();
            failureReason = "invalid-qq-event";
            return false;
        }

        var isGroup = eventType is "GROUP_AT_MESSAGE_CREATE" or "GROUP_MESSAGE_CREATE";
        var isPrivate = eventType == "C2C_MESSAGE_CREATE";
        if (isGroup || isPrivate)
        {
            var messageId = GetString(data, "id");
            data.TryGetProperty("author", out var author);
            var userId = GetString(author, isGroup ? "member_openid" : "user_openid") ?? GetString(author, "id");
            var targetId = isGroup ? GetString(data, "group_openid") ?? GetString(data, "group_id") : userId;
            if (string.IsNullOrWhiteSpace(messageId) || string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(targetId))
            {
                document.Dispose();
                failureReason = "missing-qq-message-identity";
                return false;
            }

            if (author.TryGetProperty("bot", out var isBot) && isBot.ValueKind == JsonValueKind.True)
            {
                document.Dispose();
                return false;
            }

            var identity = new MessageIdentity(targetId, isGroup ? MessageType.Channel : MessageType.Private);
            var rawContent = GetString(data, "content")?.Trim() ?? "";
            messageContext.MessageIdentity = identity;
            messageContext.MessageUserIdentity = new MessageUserIdentity(identity, userId);
            messageContext.MessageId = messageId;
            messageContext.RawMessage = rawContent;
            messageContext.TextMessage = rawContent;
        }
        else
        {
            messageContext.MessageIdentity = MessageIdentity.NoticeMessage;
            messageContext.RawMessage = "";
        }

        messageContext.RawJsonDocument = document;
        messageContext.EventType = eventType;
        messageContext.EventId = GetString(root, "id");
        messageContext.ReceivedTime = DateTimeOffset.TryParse(GetString(data, "timestamp"), CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var timestamp) ? timestamp : inboundMessage.ReceivedAt;
        return true;
    }

    private static string? GetString(JsonElement element, string property)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
