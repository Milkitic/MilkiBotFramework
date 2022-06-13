namespace MilkiBotFramework.Messaging.RichMessages;

public class Text : IRichMessage
{
    public Text(string content) => Content = content;
    public string Content { get; set; }

    public static implicit operator Text(string content) => new(content);
    public virtual ValueTask<string> EncodeAsync() => ValueTask.FromResult(Content);
    public override string ToString() => Content;
}