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
    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath) || !string.IsNullOrWhiteSpace(ImageUrl);

    public bool HasText => !string.IsNullOrWhiteSpace(Content);
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ImagePath { get; set; }
    public string? ImageSource => !string.IsNullOrWhiteSpace(ImagePath) ? ImagePath : ImageUrl;

    public string ImageTitle
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ImagePath))
            {
                return $"[图片] {Path.GetFileName(ImagePath)}";
            }

            return !string.IsNullOrWhiteSpace(ImageUrl)
                ? $"[图片] {ImageUrl}"
                : "[图片]";
        }
    }

    public string? ImageUrl { get; set; }
    public bool IsBotMessage { get; set; } = false;
    public string SenderId { get; set; } = "";
    public string SenderName { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}