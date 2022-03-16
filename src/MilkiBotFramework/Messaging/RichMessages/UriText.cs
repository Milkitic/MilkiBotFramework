namespace MilkiBotFramework.Messaging.RichMessages;

public class UriText : IRichMessage
{
    public UriText(string content) => Content = content;
    public string Content { get; set; }
    public virtual async ValueTask<string> EncodeAsync() => Content;
    public override string ToString() => "[链接]";
}