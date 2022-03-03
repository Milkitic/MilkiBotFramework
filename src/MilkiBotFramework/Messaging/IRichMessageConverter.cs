using System;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

public interface IRichMessageConverter
{
    ValueTask<string> EncodeAsync(IRichMessage message);
    RichMessage Decode(ReadOnlyMemory<char> message);
}