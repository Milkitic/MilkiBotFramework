namespace MilkiBotFramework.Messaging;

public record MessageContext
{
    public string RawTextMessage { get; }
    public string? MessageId { get; set; }
    public string? UserId { get; set; }
    public MessageIdentity? Identity { get; set; }

    public MessageContext(string rawTextMessage)
    {
        RawTextMessage = rawTextMessage;
    }
}