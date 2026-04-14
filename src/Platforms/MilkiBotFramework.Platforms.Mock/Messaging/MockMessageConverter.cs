using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.Mock.Messaging;

/// <summary>
///     Mock 平台默认消息转换器
/// </summary>
public class MockMessageConverter : IRichMessageConverter
{
    public ValueTask<string> EncodeAsync(IRichMessage message)
    {
        if (message is RichMessage richMessage)
        {
            var text = string.Join("", richMessage.OfType<Text>().Select(m => m.Content));
            return ValueTask.FromResult(text);
        }

        if (message is Text textMessage)
        {
            return ValueTask.FromResult(textMessage.Content);
        }

        return ValueTask.FromResult(string.Empty);
    }

    public RichMessage Decode(ReadOnlyMemory<char> data)
    {
        // Mock 平台简单地当作纯文本处理
        var text = data.ToString();
        return new RichMessage(new Text(text));
    }
}