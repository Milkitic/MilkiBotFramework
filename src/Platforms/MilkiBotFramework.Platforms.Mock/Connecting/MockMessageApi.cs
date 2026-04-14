using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.Mock.Messaging;
using SixLabors.ImageSharp;

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
    private static readonly object SentMessagesLock = new();

    /// <summary>
    ///     每次发送消息后触发，供 UI 订阅实时显示
    /// </summary>
    public static event Action<MockMessage>? MessageSent;

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
        return SendMessagesAsync(isGroupMessage: false, identityId: userId, message, richMessage);
    }

    public Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage,
        MessageContext messageContext, string? subChannelId)
    {
        return SendMessagesAsync(isGroupMessage: true, identityId: channelId, message, richMessage);
    }

    private async Task<string> SendMessagesAsync(bool isGroupMessage, string identityId, string plainText,
        IRichMessage? richMessage)
    {
        var messages = await BuildMessagesAsync(isGroupMessage, identityId, plainText, richMessage);

        if (messages.Count == 0)
        {
            var fallback = CreateTextMessage(isGroupMessage, identityId, plainText);
            AddSentMessage(fallback);
            return fallback.Id;
        }

        foreach (var mockMessage in messages)
        {
            AddSentMessage(mockMessage);
        }

        return messages[^1].Id;
    }

    private async Task<List<MockMessage>> BuildMessagesAsync(bool isGroupMessage, string identityId, string plainText,
        IRichMessage? richMessage)
    {
        var messages = new List<MockMessage>();

        if (richMessage is RichMessage rich)
        {
            foreach (var segment in rich)
            {
                await AppendSegmentAsync(messages, isGroupMessage, identityId, segment);
            }
        }
        else if (richMessage != null)
        {
            await AppendSegmentAsync(messages, isGroupMessage, identityId, richMessage);
        }

        if (messages.Count == 0 && !string.IsNullOrWhiteSpace(plainText))
        {
            messages.Add(CreateTextMessage(isGroupMessage, identityId, plainText));
        }

        return messages;
    }

    private async Task AppendSegmentAsync(List<MockMessage> messages, bool isGroupMessage, string identityId,
        IRichMessage segment)
    {
        switch (segment)
        {
            case Reply:
                return;
            case At at:
                messages.Add(CreateTextMessage(isGroupMessage, identityId, $"@{at.UserId}"));
                return;
            case Text text when !string.IsNullOrWhiteSpace(text.Content):
                messages.Add(CreateTextMessage(isGroupMessage, identityId, text.Content));
                return;
            case FileImage fileImage:
                messages.Add(CreateImageMessage(isGroupMessage, identityId, fileImage.Path, null));
                return;
            case LinkImage linkImage:
                messages.Add(CreateImageMessage(isGroupMessage, identityId, null, linkImage.Uri));
                return;
            case MemoryImage memoryImage:
                var savedPath = await SaveMemoryImageToTempAsync(memoryImage);
                messages.Add(CreateImageMessage(isGroupMessage, identityId, savedPath, null));
                return;
        }

        var encoded = await segment.EncodeAsync();
        if (!string.IsNullOrWhiteSpace(encoded))
        {
            messages.Add(CreateTextMessage(isGroupMessage, identityId, encoded));
        }
    }

    private static async Task<string> SaveMemoryImageToTempAsync(MemoryImage memoryImage)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "MilkiBotFramework", "mock-images");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.png");
        await memoryImage.ImageSource.SaveAsPngAsync(filePath);
        return filePath;
    }

    private MockMessage CreateTextMessage(bool isGroupMessage, string identityId, string content)
    {
        var config = _options.Config;
        return new MockMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = config.BotUserId,
            SenderName = config.BotUserName,
            Content = content,
            Timestamp = DateTimeOffset.Now,
            IsBotMessage = true,
            GroupId = isGroupMessage ? identityId : null,
            GroupName = isGroupMessage ? config.GroupName : null
        };
    }

    private MockMessage CreateImageMessage(bool isGroupMessage, string identityId, string? imagePath,
        string? imageUrl)
    {
        var config = _options.Config;
        return new MockMessage
        {
            Id = Guid.NewGuid().ToString(),
            SenderId = config.BotUserId,
            SenderName = config.BotUserName,
            Content = string.Empty,
            ImagePath = imagePath,
            ImageUrl = imageUrl,
            Timestamp = DateTimeOffset.Now,
            IsBotMessage = true,
            GroupId = isGroupMessage ? identityId : null,
            GroupName = isGroupMessage ? config.GroupName : null
        };
    }

    private static void AddSentMessage(MockMessage message)
    {
        lock (SentMessagesLock)
        {
            SentMessages.Add(message);
        }

        MessageSent?.Invoke(message);
    }

    /// <summary>
    ///     发送群聊消息
    /// </summary>
    public void SendGroupMessage(string groupId, string message)
    {
        AddSentMessage(CreateTextMessage(isGroupMessage: true, groupId, message));
    }

    /// <summary>
    ///     发送私聊消息
    /// </summary>
    public void SendPrivateMessage(string userId, string message)
    {
        AddSentMessage(CreateTextMessage(isGroupMessage: false, userId, message));
    }

    /// <summary>
    ///     获取发送历史（用于 UI 展示）
    /// </summary>
    public static IReadOnlyList<MockMessage> GetSentMessages()
    {
        lock (SentMessagesLock)
        {
            return SentMessages.ToList().AsReadOnly();
        }
    }

    /// <summary>
    ///     清空发送历史
    /// </summary>
    public static void ClearSentMessages()
    {
        lock (SentMessagesLock)
        {
            SentMessages.Clear();
        }
    }
}