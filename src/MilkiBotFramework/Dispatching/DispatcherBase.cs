using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContractsManaging;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

public abstract class DispatcherBase<TMessageContext> : IDispatcher
    where TMessageContext : MessageContext
{
    public event Func<TMessageContext, Task>? PublicMessageReceived;
    public event Func<TMessageContext, Task>? PrivateMessageReceived;
    public event Func<TMessageContext, Task>? SystemMessageReceived;
    public event Func<TMessageContext, Task>? MetaMessageReceived;

    private readonly IConnector _connector;
    private readonly IContractsManager _contractsManager;
    private readonly ILogger _logger;

    public DispatcherBase(IConnector connector, IContractsManager contractsManager, ILogger logger)
    {
        _connector = connector;
        _contractsManager = contractsManager;
        _logger = logger;
        _connector.RawMessageReceived += Connector_RawMessageReceived;
    }

    private async Task Connector_RawMessageReceived(string rawMessage)
    {
        try
        {
            var messageContext = CreateMessageContext(rawMessage);
            await HandleMessageCore(messageContext);
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

        messageContext.Identity = messageIdentity;

        switch (messageIdentity!.MessageType)
        {
            case MessageType.Private:
                if (PrivateMessageReceived != null) await PrivateMessageReceived.Invoke(messageContext);
                break;
            case MessageType.Public:
                _contractsManager.TryGetChannelInfoByMessageContext(messageIdentity, out var channelInfo, out var memberInfo);
                if (PublicMessageReceived != null) await PublicMessageReceived.Invoke(messageContext);
                break;
            case MessageType.System:
                if (SystemMessageReceived != null) await SystemMessageReceived.Invoke(messageContext);
                break;
            case MessageType.Meta:
                if (MetaMessageReceived != null) await MetaMessageReceived.Invoke(messageContext);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        _logger.LogInformation($"Received data: \r\n{messageContext}");
    }

    protected abstract TMessageContext CreateMessageContext(string rawMessage);

    protected abstract bool TryGetIdentityByRawMessage(TMessageContext messageContext,
        [NotNullWhen(true)] out MessageIdentity? messageIdentity,
        out string? strIdentity);

    event Func<MessageContext, Task>? IDispatcher.PublicMessageReceived
    {
        add => PublicMessageReceived += value;
        remove => PublicMessageReceived -= value;
    }

    event Func<MessageContext, Task>? IDispatcher.PrivateMessageReceived
    {
        add => PrivateMessageReceived += value;
        remove => PrivateMessageReceived -= value;
    }

    event Func<MessageContext, Task>? IDispatcher.SystemMessageReceived
    {
        add => SystemMessageReceived += value;
        remove => SystemMessageReceived -= value;
    }

    event Func<MessageContext, Task>? IDispatcher.MetaMessageReceived
    {
        add => MetaMessageReceived += value;
        remove => MetaMessageReceived -= value;
    }
}