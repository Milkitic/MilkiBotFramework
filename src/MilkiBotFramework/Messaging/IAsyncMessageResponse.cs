using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Messaging;

public interface IAsyncMessageResponse
{
    string MessageId { get; }
    string TextMessage { get; }
    DateTimeOffset ReceivedTime { get; }
    RichMessage GetRichMessage();
    CommandLineResult? GetCommandLineResult();
}