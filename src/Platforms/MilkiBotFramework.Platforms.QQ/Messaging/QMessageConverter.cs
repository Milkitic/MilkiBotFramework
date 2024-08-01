using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.QQ.Messaging;

public class QMessageConverter : IRichMessageConverter
{
    public ValueTask<string> EncodeAsync(IRichMessage message)
    {
        throw new NotImplementedException();
    }

    public RichMessage Decode(ReadOnlyMemory<char> message)
    {
        throw new NotImplementedException();
    }
}