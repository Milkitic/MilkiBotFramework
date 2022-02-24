namespace MilkiBotFramework.Messaging.RichMessages;

public class Text : IRichMessage
{
    public Text(string content) => Content = content;
    public string Content { get; set; }

    public static implicit operator Text(string content) => new(content);
    public virtual string Encode() => Content;
    public override string ToString() => Content;
}