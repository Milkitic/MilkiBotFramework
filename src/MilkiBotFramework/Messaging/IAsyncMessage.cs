using System;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

public interface IAsyncMessage
{
    Task<IAsyncMessageResponse> GetNextMessageAsync(int seconds = 10);
    Task<IAsyncMessageResponse> GetNextMessageAsync(TimeSpan dueTime);
}

public interface IAsyncMessageResponse
{
    string MessageId { get; }
    string TextMessage { get; }
    DateTimeOffset ReceivedTime { get; }
    RichMessage GetRichMessage();
}