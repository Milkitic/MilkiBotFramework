using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

public sealed class PlatformRichMessageConverterRouter : IRichMessageConverter, IPlatformRichMessageConverterRouter
{
    private readonly IPlatformRichMessageConverter[] _converters;

    public PlatformRichMessageConverterRouter(IEnumerable<IPlatformRichMessageConverter> converters)
    {
        _converters = converters.ToArray();
    }

    public ValueTask<string> EncodeAsync(IRichMessage message)
    {
        return _converters.FirstOrDefault()?.EncodeAsync(message)
               ?? ValueTask.FromResult(string.Empty);
    }

    public RichMessage Decode(ReadOnlyMemory<char> message)
    {
        return _converters.FirstOrDefault()?.Decode(message) ?? new RichMessage(new Text(message.ToString()));
    }

    public ValueTask<string> EncodeAsync(MessageContext messageContext, IRichMessage message)
    {
        return Resolve(messageContext).EncodeAsync(message);
    }

    public RichMessage Decode(MessageContext messageContext, ReadOnlyMemory<char> message)
    {
        return Resolve(messageContext).Decode(message);
    }

    private IPlatformRichMessageConverter Resolve(MessageContext messageContext)
    {
        return _converters.FirstOrDefault(converter =>
                   string.Equals(converter.PlatformId, messageContext.PlatformId, StringComparison.OrdinalIgnoreCase))
               ?? _converters.FirstOrDefault()
               ?? throw new InvalidOperationException("No rich message converter registered.");
    }
}