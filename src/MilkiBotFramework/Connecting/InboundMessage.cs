namespace MilkiBotFramework.Connecting;

public sealed class InboundMessage
{
    private InboundMessage(string? rawText, object? payload, string? transport)
    {
        RawText = rawText;
        Payload = payload;
        Transport = transport;
        ReceivedAt = DateTimeOffset.UtcNow;
    }

    public string? RawText { get; }
    public object? Payload { get; }
    public string? Transport { get; }
    public DateTimeOffset ReceivedAt { get; }

    public TPayload? GetPayload<TPayload>() where TPayload : class
    {
        return Payload as TPayload;
    }

    public static InboundMessage FromRawText(string rawText, string? transport = null)
    {
        return new InboundMessage(rawText, null, transport);
    }

    public static InboundMessage FromPayload<TPayload>(TPayload payload, string? rawText = null,
        string? transport = null)
        where TPayload : class
    {
        return new InboundMessage(rawText, payload, transport);
    }
}