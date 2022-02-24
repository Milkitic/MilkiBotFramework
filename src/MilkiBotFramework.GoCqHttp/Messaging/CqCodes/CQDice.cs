using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 掷骰子魔法表情
    /// </summary>
    public class CQDice : IRichMessage
    {
        public override string ToString() => "[掷色子]";
        public string Encode() => "[CQ:dice]";
    }
}