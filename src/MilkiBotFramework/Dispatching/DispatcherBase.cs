using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContractsManaging;
using MilkiBotFramework.ContractsManaging.Models;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

public abstract class DispatcherBase<TMessageContext> : IDispatcher
    where TMessageContext : MessageContext
{
    public event Func<TMessageContext, ChannelInfo, MemberInfo, Task>? ChannelMessageReceived;
    public event Func<TMessageContext, PrivateInfo, Task>? PrivateMessageReceived;
    public event Func<TMessageContext, Task>? NoticeMessageReceived;
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
                var privateResult = await _contractsManager.TryGetPrivateInfoByMessageContext(messageIdentity);
                if (privateResult.IsSuccess && PrivateMessageReceived != null)
                {
                    await PrivateMessageReceived.Invoke(messageContext, privateResult.PrivateInfo);
                }
                break;
            case MessageType.Public:
                var channelResult =
                    await _contractsManager.TryGetChannelInfoByMessageContext(messageIdentity, messageContext.UserId);
                if (channelResult.IsSuccess && ChannelMessageReceived != null)
                {
                    await ChannelMessageReceived.Invoke(messageContext, channelResult.ChannelInfo, channelResult.MemberInfo);
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

        _logger.LogDebug($"Received data: \r\n{messageContext}");
    }

    protected abstract TMessageContext CreateMessageContext(string rawMessage);

    protected abstract bool TryGetIdentityByRawMessage(TMessageContext messageContext,
        [NotNullWhen(true)] out MessageIdentity? messageIdentity,
        out string? strIdentity);

    event Func<MessageContext, ChannelInfo, MemberInfo, Task>? IDispatcher.ChannelMessageReceived
    {
        add => ChannelMessageReceived += value;
        remove => ChannelMessageReceived -= value;
    }

    event Func<MessageContext, PrivateInfo, Task>? IDispatcher.PrivateMessageReceived
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