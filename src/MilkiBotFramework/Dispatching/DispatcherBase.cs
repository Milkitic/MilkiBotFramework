using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

public abstract class DispatcherBase<TMessageContext> : IDispatcher
    where TMessageContext : MessageContext
{
    private readonly IConnector _connector;
    private readonly IContactsManager _contactsManager;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly EventBus _eventBus;

    public DispatcherBase(IConnector connector,
        IContactsManager contactsManager,
        ILogger logger,
        IServiceProvider serviceProvider,
        EventBus eventBus)
    {
        _connector = connector;
        _contactsManager = contactsManager;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _connector.RawMessageReceived += Connector_RawMessageReceived;
    }

    public async Task InvokeRawMessageReceived(string rawMessage)
    {
        await Connector_RawMessageReceived(rawMessage);
    }

    private async Task Connector_RawMessageReceived(string rawMessage)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var scope = _serviceProvider.CreateScope();
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
                if (privateResult.IsSuccess)
                {
                    messageContext.PrivateInfo = privateResult.PrivateInfo;
                }
                break;
            case MessageType.Channel:
                var channelResult =
                    await _contactsManager.TryGetChannelInfoByMessageContext(messageIdentity, messageContext.MessageUserIdentity!.UserId);
                if (channelResult.IsSuccess)
                {
                    messageContext.ChannelInfo = channelResult.ChannelInfo;
                    messageContext.MemberInfo = channelResult.MemberInfo;
                }
                break;
            case MessageType.Notice:
                break;
            case MessageType.Meta:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        await _eventBus.PublishAsync(new DispatchMessageEvent(messageContext, messageIdentity.MessageType));
        //_logger.LogDebug($"Received data: \r\n{messageContext}");
    }

    protected abstract bool TrySetTextMessage(TMessageContext messageContext);

    protected abstract bool TryGetIdentityByRawMessage(TMessageContext messageContext,
        [NotNullWhen(true)] out MessageIdentity? messageIdentity,
        out string? strIdentity);
}