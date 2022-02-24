namespace MilkiBotFramework.Messaging.RichMessages;

public class At : IRichMessage
{
    public At(string userId) => UserId = userId;
    public string UserId { get; set; }
    public virtual string Encode() => $"[At {UserId}]";
}