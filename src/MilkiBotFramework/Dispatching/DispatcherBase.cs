using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
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
    private readonly IMessageContextEnricher _messageContextEnricher;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly EventBus _eventBus;

    public DispatcherBase(IConnector connector,
        IMessageContextEnricher messageContextEnricher,
        ILogger logger,
        IServiceProvider serviceProvider,
        EventBus eventBus)
    {
        _connector = connector;
        _messageContextEnricher = messageContextEnricher;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _eventBus = eventBus;
        _connector.MessageReceived += Connector_MessageReceived;
    }

    public async Task InvokeMessageReceived(InboundMessage inboundMessage)
    {
        await Connector_MessageReceived(inboundMessage);
    }

    private async Task Connector_MessageReceived(InboundMessage inboundMessage)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var scope = _serviceProvider.CreateScope();
            var messageContext = (TMessageContext)scope.ServiceProvider.GetService(typeof(TMessageContext))!;
            messageContext.InboundMessage = inboundMessage;
            if (await HandleMessageCore(messageContext, inboundMessage))
            {
                _logger.LogDebug($"Total dispatching elapsed: {sw.Elapsed.TotalMilliseconds:N1}ms");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurs while dispatching message.");
        }
    }

    private async Task<bool> HandleMessageCore(TMessageContext messageContext, InboundMessage inboundMessage)
    {
        var success = TryPopulateMessageContext(messageContext, inboundMessage, out var failureReason);
        if (!success)
        {
            if (failureReason == null)
            {
                return false;
            }

            _logger.LogWarning("Failed to normalize inbound message: " + failureReason);
            return true;
        }

        await _messageContextEnricher.EnrichAsync(messageContext);
        await _eventBus.PublishAsync(new DispatchMessageEvent(messageContext));
        return true;
    }

    protected abstract bool TryPopulateMessageContext(TMessageContext messageContext,
        InboundMessage inboundMessage,
        out string? failureReason);
}