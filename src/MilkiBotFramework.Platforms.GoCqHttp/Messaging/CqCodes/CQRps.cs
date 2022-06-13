using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 猜拳魔法表情
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class CQRps : IRichMessage
    {
        private CQRps()
        {
        }

        public static CQRps Instance { get; } = new();
        public override string ToString() => "[猜拳]";
        public ValueTask<string> EncodeAsync() => ValueTask.FromResult("[CQ:rps]");
    }
}