using System;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

public interface IAsyncMessageResponse
{
    string MessageId { get; }
    string TextMessage { get; }
    DateTimeOffset ReceivedTime { get; }
    RichMessage GetRichMessage();
}