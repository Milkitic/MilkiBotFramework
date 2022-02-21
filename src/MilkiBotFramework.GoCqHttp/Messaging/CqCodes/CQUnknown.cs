namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    public class CQUnknown : CQCode
    {
        public string Type { get; }
        public string Content { get; }

        public CQUnknown(string type, string content)
        {
            Type = type;
            Content = content;
        }

        public override string ToString() => $"[不支持的消息:{Type}]";
    }
}