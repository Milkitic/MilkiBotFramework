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
public abstract class DispatcherBase<TMessageContext> : IPlatformDispatcher
    where TMessageContext : MessageContext
{
    private readonly IMessageContextEnricher _messageContextEnricher;
    private readonly MessageDispatchCoordinator _messageDispatchCoordinator;
    private readonly ILogger _logger;
    private readonly IServiceProvider _serviceProvider;

    public abstract string PlatformId { get; }

    public DispatcherBase(IMessageContextEnricher messageContextEnricher,
        MessageDispatchCoordinator messageDispatchCoordinator,
        ILogger logger,
        IServiceProvider serviceProvider)
    {
        _messageContextEnricher = messageContextEnricher;
        _messageDispatchCoordinator = messageDispatchCoordinator;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public virtual bool CanDispatch(InboundMessage inboundMessage)
    {
        return string.Equals(inboundMessage.Transport, PlatformId, StringComparison.OrdinalIgnoreCase);
    }

    public async Task InvokeMessageReceived(InboundMessage inboundMessage)
    {
        await DispatchCoreAsync(inboundMessage);
    }

    private async Task DispatchCoreAsync(InboundMessage inboundMessage)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            using var scope = _serviceProvider.CreateScope();
            var messageContext = (TMessageContext)scope.ServiceProvider.GetService(typeof(TMessageContext))!;
            messageContext.InboundMessage = inboundMessage;
            messageContext.PlatformId = PlatformId;
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