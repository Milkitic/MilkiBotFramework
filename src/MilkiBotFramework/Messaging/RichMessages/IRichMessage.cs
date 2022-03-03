namespace MilkiBotFramework.Messaging.RichMessages;

public interface IRichMessage
{
    ValueTask<string> EncodeAsync();
}