using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

public interface IPlatformRichMessageConverterRouter
{
    ValueTask<string> EncodeAsync(MessageContext messageContext, IRichMessage message);
    RichMessage Decode(MessageContext messageContext, ReadOnlyMemory<char> message);
}