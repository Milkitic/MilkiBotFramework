using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.Mock.Connecting;
using MilkiBotFramework.Platforms.Mock.Messaging;

namespace MilkiBotFramework.Platforms.Mock.Dispatching;

/// <summary>
///     Mock 平台消息分发器 - 将虚拟消息转换为标准消息上下文
/// </summary>
public class MockDispatcher : DispatcherBase<MockMessageContext>
{
    public MockDispatcher(
        IConnector connector,
        IContactsManager contactsManager,
        ILogger<MockDispatcher> logger,
        IServiceProvider serviceProvider,
        BotOptions botOptions,
        EventBus eventBus)
        : base(connector, contactsManager, logger, serviceProvider, botOptions, eventBus)
    {
    }

    protected override bool TryGetIdentityByRawMessage(
        MockMessageContext messageContext,
        [NotNullWhen(true)] out MessageIdentity? messageIdentity,
        out string? strIdentity)
    {
        var messageId = messageContext.RawTextMessage;
        strIdentity = messageId;

        if (messageId != null && MockConnector.MessageCache.TryGetValue(messageId, out var mockMessage))
        {
            messageContext.RawMessage = mockMessage;
            messageContext.MessageId = mockMessage.Id;
            messageContext.ReceivedTime = mockMessage.Timestamp;

            // 判断消息类型：群聊或私聊
            var messageType = !string.IsNullOrEmpty(mockMessage.GroupId)
                ? MessageType.Channel
                : MessageType.Private;

            if (messageType == MessageType.Channel)
            {
                // 群聊消息
                messageIdentity = new MessageIdentity(
                    mockMessage.GroupId,
                    mockMessage.GroupId, // 简化处理
                    MessageType.Channel);

                messageContext.MessageUserIdentity = new MessageUserIdentity(
                    messageIdentity,
                    mockMessage.SenderId);
            }
            else
            {
                // 私聊消息
                messageIdentity = new MessageIdentity(
                    mockMessage.SenderId,
                    MessageType.Private);

                messageContext.MessageUserIdentity = new MessageUserIdentity(
                    messageIdentity,
                    mockMessage.SenderId);
            }

            return true;
        }

        messageIdentity = null;
        return false;
    }

    protected override bool TrySetTextMessage(MockMessageContext messageContext)
    {
        if (messageContext.RawMessage != null)
        {
            messageContext.TextMessage = messageContext.RawMessage.Content;
            return true;
        }

        return false;
    }
}