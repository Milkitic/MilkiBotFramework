using System.ComponentModel;

namespace MilkiBotFramework.Platforms.GoCqHttp
{
    public class GoCqBotOptions : BotOptions
    {
        [Description("go-cqhttp连接设置")]
        public GoCqConnection Connection { get; set; } = new();
    }
}
