using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.QQ.Messaging;

/// <summary>处理 QQ 适配器的框架富消息与文本转换。</summary>
public class QMessageConverter : IPlatformRichMessageConverter
{
    /// <summary>框架中 QQ 官方平台的标识。</summary>
    public string PlatformId => PlatformIds.Qq;

    /// <summary>使用框架消息段的编码生成文本表示。</summary>
    /// <param name="message">待编码的富消息。</param>
    /// <returns>消息的框架文本表示。</returns>
    public async ValueTask<string> EncodeAsync(IRichMessage message)
    {
        return await message.EncodeAsync();
    }

    /// <summary>将 QQ 原始文本包装为一个框架文本段。</summary>
    /// <param name="message">QQ 消息文本。</param>
    /// <returns>包含给定文本的富消息。</returns>
    public RichMessage Decode(ReadOnlyMemory<char> message)
    {
        return new RichMessage(new Text(message.ToString()));
    }
}
