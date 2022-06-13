using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

/// <summary>
/// 表示一个类，用以分发处理后的消息。
/// <para>该类可处理原始的字符串消息，将结果以<see cref="EventBus"/>的途径分发。</para>
/// </summary>
/// <typeparam name="TMessageContext"><see cref="MessageContext"/>类型</typeparam>
public abstract class DispatcherBase<TMessageContext> : IDispatcher
    where TMessageContext : MessageContext
{
    private readonly IConnector _connector;
    private readonly IContactsManager _contactsManager;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly BotOptions _botOptions;
    private readonly EventBus _eventBus;

    public DispatcherBase(IConnector connector,
        IContactsManager contactsManager,
        ILogger logger,
        IServiceProvider serviceProvider,
        BotOptions botOptions,
        EventBus eventBus)
    {
        _connector = connector;
        _contactsManager = contactsManager;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _botOptions = botOptions;
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
        switch (messageIdentity!.MessageType)
        {
            case MessageType.Private:
                if (messageIdentity.Id == null) throw new ArgumentNullException(nameof(messageIdentity.Id));
                var privateResult = await _contactsManager.TryGetOrAddPrivateInfo(messageIdentity.Id);
                if (privateResult.IsSuccess)
                {
                    if (_botOptions.RootAccounts.Contains(messageIdentity.Id))
                        messageContext.Authority = MessageAuthority.Root;
                    else
                        messageContext.Authority = MessageAuthority.Public;
                    messageContext.PrivateInfo = privateResult.PrivateInfo;
                }
                else
                {
                    _logger.LogWarning("Failed to fill PrivateInfo automatically. This may leads to further plugin errors.");
                }

                break;
            case MessageType.Channel:
                if (messageIdentity.Id == null) throw new ArgumentNullException(nameof(MessageIdentity.Id));
                var userId = messageContext.MessageUserIdentity?.UserId;
                if (userId == null) throw new ArgumentNullException(nameof(MessageUserIdentity.UserId));
                var channelResult = await _contactsManager.TryGetOrAddChannelInfo(messageIdentity.Id, messageIdentity.SubId);
                var memberResult = await _contactsManager.TryGetOrAddMemberInfo(messageIdentity.Id, userId);
                if (channelResult.IsSuccess)
                    messageContext.ChannelInfo = channelResult.ChannelInfo;
                else
                    _logger.LogWarning("Failed to ChannelInfo automatically. This may leads to further plugin errors.");
                if (memberResult.IsSuccess)
                {
                    if (_botOptions.RootAccounts.Contains(userId))
                        messageContext.Authority = MessageAuthority.Root;
                    else if (memberResult.MemberInfo!.MemberRole is MemberRole.Admin)
                        messageContext.Authority = MessageAuthority.Admin;
                    else if (memberResult.MemberInfo!.MemberRole is MemberRole.SubAdmin)
                        messageContext.Authority = MessageAuthority.SubAdmin;
                    else
                        messageContext.Authority = MessageAuthority.Public;

                    messageContext.MemberInfo = memberResult.MemberInfo;
                }
                else
                {
                    _logger.LogWarning("Failed to MemberInfo automatically. This may leads to further plugin errors.");
                }

                break;
            case MessageType.Notice:
                break;
            case MessageType.Meta:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        TrySetTextMessage(messageContext);
        await _eventBus.PublishAsync(new DispatchMessageEvent(messageContext, messageIdentity.MessageType));
    }

    protected abstract bool TrySetTextMessage(TMessageContext messageContext);

    protected abstract bool TryGetIdentityByRawMessage(TMessageContext messageContext,
        [NotNullWhen(true)] out MessageIdentity? messageIdentity,
        out string? strIdentity);
}