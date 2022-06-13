using System.ComponentModel;
using MilkiBotFramework.Plugining.Configuration;

namespace DemoBot;

public class TestConfiguration : ConfigurationBase
{
    [Description("Testing for comment in config file!")]
    public string Key1 { get; set; } = "";
    [Description("这是一条注释，这是个数字！")]
    public int Key2 { get; set; }
}