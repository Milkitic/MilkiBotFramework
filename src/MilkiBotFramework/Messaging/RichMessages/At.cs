namespace MilkiBotFramework.Messaging.RichMessages;

public class At : IRichMessage
{
    public At(long userId) => UserId = userId;
    public long UserId { get; set; }
}