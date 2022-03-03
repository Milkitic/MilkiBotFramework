using System.Threading.Tasks;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 猜拳魔法表情
    /// </summary>
    public class CQRps : IRichMessage
    {
        private CQRps()
        {
        }

        public static CQRps Instance { get; } = new();
        public override string ToString() => "[猜拳]";
        public async ValueTask<string> EncodeAsync() => "[CQ:rps]";
    }
}