using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 掷骰子魔法表情
    /// </summary>
    public class CQDice : IRichMessage
    {
        private CQDice()
        {
        }

        public static CQDice Instance { get; } = new();
        public override string ToString() => "[掷色子]";
        public string Encode() => "[CQ:dice]";
    }
}