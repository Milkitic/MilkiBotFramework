using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Platforms.Mock.Messaging;

/// <summary>
///     Mock 平台消息上下文
/// </summary>
public class MockMessageContext : MessageContext
{
    public MockMessageContext(IRichMessageConverter richMessageConverter) : base(richMessageConverter)
    {
    }

    /// <summary>
    ///     原始消息对象（模拟使用）
    /// </summary>
    public MockMessage? RawMessage { get; set; }
}

/// <summary>
///     虚拟消息对象
/// </summary>
public class MockMessage
{
    public string Content { get; set; } = "";
    public string? GroupId { get; set; } = null;
    public string? GroupName { get; set; } = null;
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public bool IsBotMessage { get; set; } = false;
    public string SenderId { get; set; } = "";
    public string SenderName { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}