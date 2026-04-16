using System.Collections.Concurrent;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Plugining;

public sealed class AsyncMessageSessionManager
{
    private readonly ConcurrentDictionary<MessageUserIdentity, AsyncMessage> _sessions = new();
    private readonly IRichMessageConverter _richMessageConverter;
    private readonly ICommandLineAnalyzer _commandLineAnalyzer;

    public AsyncMessageSessionManager(IRichMessageConverter richMessageConverter,
        ICommandLineAnalyzer commandLineAnalyzer)
    {
        _richMessageConverter = richMessageConverter;
        _commandLineAnalyzer = commandLineAnalyzer;
    }

    public bool TryConsume(MessageContext messageContext)
    {
        if (messageContext.MessageUserIdentity == null ||
            !_sessions.TryRemove(messageContext.MessageUserIdentity, out var asyncMessage))
        {
            return false;
        }

        asyncMessage.SetMessage(new AsyncMessageResponse(messageContext.MessageId!,
            messageContext.TextMessage!,
            messageContext.ReceivedTime,
            s => _richMessageConverter is IPlatformRichMessageConverterRouter router
                ? router.Decode(messageContext, s.AsMemory())
                : _richMessageConverter.Decode(s.AsMemory()),
            s =>
            {
                _commandLineAnalyzer.TryAnalyze(s, out var result, out var ex);
                if (ex != null) throw ex;
                return result;
            }));
        return true;
    }

    internal void Register(MessageUserIdentity messageUserIdentity, AsyncMessage asyncMessage)
    {
        _sessions.AddOrUpdate(messageUserIdentity, asyncMessage, (_, _) => asyncMessage);
    }

    internal void Clear(MessageUserIdentity? messageUserIdentity)
    {
        if (messageUserIdentity == null)
        {
            return;
        }

        _sessions.TryRemove(messageUserIdentity, out _);
    }
}