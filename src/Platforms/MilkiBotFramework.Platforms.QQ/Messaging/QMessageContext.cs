using System.Text.Json;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Platforms.QQ.Messaging;

/// <summary>QQ 官方事件在单次框架分发期间的上下文。</summary>
public class QMessageContext : MessageContext, IDisposable
{
    /// <summary>QQ 事件的原始 JSON 文档。其生命周期与当前消息作用域一致。</summary>
    public JsonDocument RawJsonDocument { get; internal set; } = null!;
    /// <summary>QQ 消息的原始文本。通知或纯附件消息可能为空字符串。</summary>
    public string RawMessage { get; internal set; } = null!;

    /// <summary>官方载荷中的事件名，即外层 <c>t</c> 字段。</summary>
    public string? EventType { get; internal set; }

    /// <summary>官方载荷中的外层事件 ID，可用于部分事件的被动回复。</summary>
    public string? EventId { get; internal set; }

    /// <summary>创建 QQ 消息上下文。</summary>
    /// <param name="richMessageConverter">从消息文本读取框架富消息的转换器。</param>
    public QMessageContext(IRichMessageConverter richMessageConverter) : base(richMessageConverter)
    {
    }

    /// <summary>释放当前上下文持有的原始 JSON 文档。</summary>
    public void Dispose()
    {
        RawJsonDocument?.Dispose();
        GC.SuppressFinalize(this);
    }
}
