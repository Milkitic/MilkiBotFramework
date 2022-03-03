using System;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Messaging;

internal class AsyncMessageResponse : IAsyncMessageResponse
{
    private readonly Func<string, RichMessage> _getRichDelegate;
    private readonly Func<string, CommandLineResult?> _getCommandLineDelegate;

    public AsyncMessageResponse(string messageId,
        string textMessage,
        DateTimeOffset receivedTime,
        Func<string, RichMessage> getRichDelegate,
        Func<string, CommandLineResult?> getCommandLineDelegate)
    {
        MessageId = messageId;
        TextMessage = textMessage;
        ReceivedTime = receivedTime;
        _getRichDelegate = getRichDelegate;
        _getCommandLineDelegate = getCommandLineDelegate;
    }

    public string MessageId { get; }
    public string TextMessage { get; }
    public DateTimeOffset ReceivedTime { get; }
    public RichMessage GetRichMessage() => _getRichDelegate(TextMessage);
    public CommandLineResult? GetCommandLineResult() => _getCommandLineDelegate(TextMessage);
}