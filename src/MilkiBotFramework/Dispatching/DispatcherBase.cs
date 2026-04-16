using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

/// <summary>
/// 表示一个类，用以分发处理后的消息。
/// <para>该类负责将平台入站消息标准化为 <see cref="MessageContext"/>，并交由消息编排器继续处理。</para>
/// </summary>
/// <typeparam name="TMessageContext"><see cref="MessageContext"/>类型</typeparam>
public abstract class DispatcherBase<TMessageContext> : IDispatcher
    where TMessageContext : MessageContext
{
    private readonly IConnector _connector;
    private readonly IMessageContextEnricher _messageContextEnricher;
    private readonly MessageDispatchCoordinator _messageDispatchCoordinator;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public DispatcherBase(IConnector connector,
        IMessageContextEnricher messageContextEnricher,
        MessageDispatchCoordinator messageDispatchCoordinator,
        ILogger logger,
        IServiceProvider serviceProvider)
    {
        _connector = connector;
        _messageContextEnricher = messageContextEnricher;
        _messageDispatchCoordinator = messageDispatchCoordinator;
        _logger = logger;
        _serviceProvider = serviceProvider;
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
        await _messageDispatchCoordinator.DispatchAsync(messageContext);
        return true;
    }

    protected abstract bool TryPopulateMessageContext(TMessageContext messageContext,
        InboundMessage inboundMessage,
        out string? failureReason);
}