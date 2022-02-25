using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

public abstract class DispatcherBase<TMessageContext> : IDispatcher
    where TMessageContext : MessageContext
{
    public event Func<TMessageContext, Task>? ChannelMessageReceived;
    public event Func<TMessageContext, Task>? PrivateMessageReceived;
    public event Func<TMessageContext, Task>? NoticeMessageReceived;
    public event Func<TMessageContext, Task>? MetaMessageReceived;

    private readonly IConnector _connector;
    private readonly IContactsManager _contactsManager;
    private readonly ILogger _logger;
    public IServiceProvider SingletonServiceProvider { get; set; }

    public DispatcherBase(IConnector connector, IContactsManager contactsManager, ILogger logger)
    {
        _connector = connector;
        _contactsManager = contactsManager;
        _logger = logger;
        _connector.RawMessageReceived += Connector_RawMessageReceived;
    }

    private async Task Connector_RawMessageReceived(string rawMessage)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var scope = SingletonServiceProvider.CreateScope();
            var messageContext = (TMessageContext)scope.ServiceProvider.GetService(typeof(TMessageContext))!;
            messageContext.RawTextMessage = rawMessage;
            await HandleMessageCore(messageContext);
            _logger.LogDebug($"Total dispatching elapsed: {sw.Elapsed.TotalMilliseconds:N1}ms");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurs while dispatching message.");
        }
    }

    private async Task HandleMessageCore(TMessageContext messageContext)
    {
        var hasIdentity = TryGetIdentityByRawMessage(messageContext, out var messageIdentity, out var strIdentity);
        if (!hasIdentity)
        {
            if (strIdentity == null)
                _logger.LogWarning("Unknown message identity.");
            else
                _logger.LogWarning("Unknown message identity: " + strIdentity);
            return;
        }

        messageContext.MessageIdentity = messageIdentity;
        TrySetTextMessage(messageContext);
        switch (messageIdentity!.MessageType)
        {
            case MessageType.Private:
                var privateResult = await _contactsManager.TryGetPrivateInfoByMessageContext(messageIdentity);
                if (privateResult.IsSuccess && PrivateMessageReceived != null)
                {
                    messageContext.PrivateInfo = privateResult.PrivateInfo;
                    await PrivateMessageReceived.Invoke(messageContext);
                }
                break;
            case MessageType.Channel:
                var channelResult =
                    await _contactsManager.TryGetChannelInfoByMessageContext(messageIdentity, messageContext.UserId);
                if (channelResult.IsSuccess && ChannelMessageReceived != null)
                {
                    messageContext.ChannelInfo = channelResult.ChannelInfo;
                    messageContext.MemberInfo = channelResult.MemberInfo;
                    await ChannelMessageReceived.Invoke(messageContext);
                }
                break;
            case MessageType.Notice:
                if (NoticeMessageReceived != null) await NoticeMessageReceived.Invoke(messageContext);
                break;
            case MessageType.Meta:
                if (MetaMessageReceived != null) await MetaMessageReceived.Invoke(messageContext);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        //_logger.LogDebug($"Received data: \r\n{messageContext}");
    }

    protected abstract bool TrySetTextMessage(TMessageContext messageContext);

    protected abstract bool TryGetIdentityByRawMessage(TMessageContext messageContext,
        [NotNullWhen(true)] out MessageIdentity? messageIdentity,
        out string? strIdentity);

    event Func<MessageContext, Task>? IDispatcher.ChannelMessageReceived
    {
        add => ChannelMessageReceived += value;
        remove => ChannelMessageReceived -= value;
    }

    event Func<MessageContext, Task>? IDispatcher.PrivateMessageReceived
    {
        add => PrivateMessageReceived += value;
        remove => PrivateMessageReceived -= value;
    }

    event Func<MessageContext, Task>? IDispatcher.SystemMessageReceived
    {
        add => NoticeMessageReceived += value;
        remove => NoticeMessageReceived -= value;
    }

    event Func<MessageContext, Task>? IDispatcher.MetaMessageReceived
    {
        add => MetaMessageReceived += value;
        remove => MetaMessageReceived -= value;
    }
}