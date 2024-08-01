using System.ComponentModel;

namespace MilkiBotFramework.Platforms.QQ;

public class QQBotOptions : BotOptions
{
    [Description("QQ API设置")]
    public QConnection Connection { get; set; } = new();
}