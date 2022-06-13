namespace MilkiBotFramework.Messaging.RichMessages;

public class At : IRichMessage
{
    public At(string userId) => UserId = userId;
    public string UserId { get; set; }
    public virtual ValueTask<string> EncodeAsync() => ValueTask.FromResult($"[At {UserId}]");
}