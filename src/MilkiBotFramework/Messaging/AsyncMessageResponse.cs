using System;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

internal class AsyncMessageResponse : IAsyncMessageResponse
{
    private readonly Func<string, RichMessage> _getRichDelegate;

    public AsyncMessageResponse(string messageId,
        string textMessage,
        DateTimeOffset receivedTime,
        Func<string, RichMessage> getRichDelegate)
    {
        MessageId = messageId;
        TextMessage = textMessage;
        ReceivedTime = receivedTime;
        _getRichDelegate = getRichDelegate;
    }

    public string MessageId { get; }
    public string TextMessage { get; }
    public DateTimeOffset ReceivedTime { get; }
    public RichMessage GetRichMessage() => _getRichDelegate(TextMessage);
}