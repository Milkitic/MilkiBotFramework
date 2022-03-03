namespace MilkiBotFramework.Messaging.RichMessages;

public class Reply : IRichMessage
{
    public Reply(string messageId) => MessageId = messageId;
    public string MessageId { get; set; }
    public virtual async ValueTask<string> EncodeAsync() => $"[Reply {MessageId}]";
}