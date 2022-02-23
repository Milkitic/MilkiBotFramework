namespace MilkiBotFramework.Messaging;

public class MessageContext
{
    public virtual MessageRequestContext Request { get; set; }
    public virtual MessageResponseContext Response { get; set; }
}