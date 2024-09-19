using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.QQ.Messaging;

public class QMessageConverter : IRichMessageConverter
{
    public async ValueTask<string> EncodeAsync(IRichMessage message)
    {
        return await message.EncodeAsync();
    }

    public RichMessage Decode(ReadOnlyMemory<char> message)
    {
        return new RichMessage(new Text(message.ToString()));
    }
}