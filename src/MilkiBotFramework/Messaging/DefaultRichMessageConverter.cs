using System;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

public class DefaultRichMessageConverter : IRichMessageConverter
{
    public string Encode(IRichMessage message)
    {
        return message.Encode();
    }

    public RichMessage Decode(ReadOnlyMemory<char> message)
    {
        return new RichMessage(new Text(message.ToString()));
    }
}