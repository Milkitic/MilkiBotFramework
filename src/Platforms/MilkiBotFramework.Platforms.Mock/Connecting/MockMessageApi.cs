using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.Mock.Messaging;

namespace MilkiBotFramework.Platforms.Mock.Connecting;

/// <summary>
///     Mock 平台消息 API - 处理消息发送的虚拟实现
/// </summary>
public class MockMessageApi : IMessageApi
{
    /// <summary>
    ///     消息发送历史记录（用于 UI 展示和测试验证）
    /// </summary>
    public static readonly List<MockMessage> SentMessages = new();

    private readonly MockBotOptions _options;

    public MockMessageApi(BotOptions botOptions, IConnector connector)
    {
        if (botOptions is not MockBotOptions mockOptions)
        {
            throw new ArgumentException("Options must be of type MockBotOptions", nameof(botOptions));
        }

        if (connector is not MockConnector mockConnector)
        {
            throw new ArgumentException("Connector must be of type MockConnector", nameof(connector));
        }

        _options = mockOptions;
    }

    public bool Supports(MessageContext messageContext)
    {
        return messageContext is MockMessageContext;
    }

    public Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage,
        MessageContext messageContext)
    {
        SendPrivateMessage(userId, message);
        return Task.FromResult(SentMessages[^1].Id);
    }

    public Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage,
        MessageContext messageContext, string? subChannelId)
    {
        SendGroupMessage(channelId, message);
        return Task.FromResult(SentMessages[^1].Id);
    }

    /// <summary>
    ///     发送群聊消息
    /// </summary>
    public void SendGroupMessage(string groupId, string message)
    {
        var config = _options.Config;
        var msg = new MockMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = config.BotUserId,
            SenderName = config.BotUserName,
            Content = message,
            Timestamp = DateTimeOffset.Now,
            IsBotMessage = true,
            GroupId = groupId,
            GroupName = config.GroupName
        };

        SentMessages.Add(msg);
    }

    /// <summary>
    ///     发送私聊消息
    /// </summary>
    public void SendPrivateMessage(string userId, string message)
    {
        var config = _options.Config;
        var msg = new MockMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = config.BotUserId,
            SenderName = config.BotUserName,
            Content = message,
            Timestamp = DateTimeOffset.Now,
            IsBotMessage = true
        };

        SentMessages.Add(msg);
    }

    /// <summary>
    ///     获取发送历史（用于 UI 展示）
    /// </summary>
    public static IReadOnlyList<MockMessage> GetSentMessages()
    {
        return SentMessages.AsReadOnly();
    }

    /// <summary>
    ///     清空发送历史
    /// </summary>
    public static void ClearSentMessages()
    {
        SentMessages.Clear();
    }
}