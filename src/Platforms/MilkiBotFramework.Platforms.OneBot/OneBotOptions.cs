using System.ComponentModel;

namespace MilkiBotFramework.Platforms.OneBot
{
    public class OneBotOptions : BotOptions
    {
        [Description("go-cqhttp连接设置")]
        public OneBotConnection Connection { get; set; } = new();
    }
}
