using System;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

public interface IRichMessageConverter
{
    string Encode(IRichMessage message);
    RichMessage Decode(ReadOnlyMemory<char> message);
}