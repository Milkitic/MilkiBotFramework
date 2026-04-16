using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.Mock.Messaging;

namespace MilkiBotFramework.Platforms.Mock.Dispatching;

/// <summary>
///     Mock 平台消息分发器 - 将虚拟消息转换为标准消息上下文
/// </summary>
public class MockDispatcher : DispatcherBase<MockMessageContext>
{
    public MockDispatcher(
        IConnector connector,
        IMessageContextEnricher messageContextEnricher,
        MessageDispatchCoordinator messageDispatchCoordinator,
        ILogger<MockDispatcher> logger,
        IServiceProvider serviceProvider)
        : base(connector, messageContextEnricher, messageDispatchCoordinator, logger, serviceProvider)
    {
    }

    protected override bool TryPopulateMessageContext(
        MockMessageContext messageContext,
        InboundMessage inboundMessage,
        out string? failureReason)
    {
        failureReason = null;

        if (inboundMessage.GetPayload<MockMessage>() is not { } mockMessage)
        {
            failureReason = inboundMessage.Payload?.GetType().Name ?? inboundMessage.RawText ?? "unknown-mock-inbound";
            return false;
        }

        messageContext.RawMessage = mockMessage;
        messageContext.MessageId = mockMessage.Id;
        messageContext.ReceivedTime = mockMessage.Timestamp;
        messageContext.TextMessage = mockMessage.Content;

        if (!string.IsNullOrEmpty(mockMessage.GroupId))
        {
            messageContext.MessageIdentity = new MessageIdentity(mockMessage.GroupId, null, MessageType.Channel);
            messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity,
                mockMessage.SenderId);
            return true;
        }

        messageContext.MessageIdentity = new MessageIdentity(mockMessage.SenderId, MessageType.Private);
        messageContext.MessageUserIdentity = new MessageUserIdentity(messageContext.MessageIdentity,
            mockMessage.SenderId);
        return true;
    }
}