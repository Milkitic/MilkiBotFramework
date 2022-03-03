using System.Threading.Tasks;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.CqCodes
{
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
        public async ValueTask<string> EncodeAsync() => Content;
    }
}