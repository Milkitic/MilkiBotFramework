using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.CqCodes
{
    // ReSharper disable once InconsistentNaming
    public class CQUnknown : IRichMessage
    {
        public string Type { get; }
        public string Content { get; }

        public CQUnknown(string type, string content)
        {
            Type = type;
            Content = content;
        }

        public override string ToString() => $"[不支持的消息:{Type}]";
        public ValueTask<string> EncodeAsync() => ValueTask.FromResult(Content);
    }
}