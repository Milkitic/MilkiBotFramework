namespace MilkiBotFramework.Messaging.RichMessages;

public class Text : IRichMessage
{
    public Text(string content) => Content = content;
    public string Content { get; set; }

    public static implicit operator Text(string content) => new Text(content);
    public string Encode() => Content;
}