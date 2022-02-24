using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 猜拳魔法表情
    /// </summary>
    public class CQRps : IRichMessage
    {
        public override string ToString() => "[猜拳]";
        public string Encode() => "[CQ:rps]";
    }
}