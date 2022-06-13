namespace MilkiBotFramework.Messaging.RichMessages;

public class UriText : IRichMessage
{
    public UriText(string content) => Content = content;
    public string Content { get; set; }
    public virtual ValueTask<string> EncodeAsync() => ValueTask.FromResult(Content);
    public override string ToString() => "[链接]";
}