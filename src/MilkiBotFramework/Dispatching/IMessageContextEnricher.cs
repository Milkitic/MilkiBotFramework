using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Dispatching;

public interface IMessageContextEnricher
{
    Task EnrichAsync(MessageContext messageContext);
}