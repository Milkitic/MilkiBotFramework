namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 掷骰子魔法表情
    /// </summary>
    public class Dice : CQCode
    {
        public Dice() { }
        public override string ToString() => "[CQ:dice]";
    }
}