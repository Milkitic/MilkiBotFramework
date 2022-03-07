using System.ComponentModel;
using MilkiBotFramework.Plugining.Configuration;

namespace MilkiBotFramework;

public class BotOptions : ConfigurationBase
{
    [Description("Root权限账号")]
    public HashSet<string> RootAccounts { get; set; } = new();
    [Description("插件目录")]
    public string PluginBaseDir { get; set; } = "./plugins";
    [Description("插件资源目录")]
    public string PluginHomeDir { get; set; } = "./homes";
    [Description("插件数据库目录")]
    public string PluginDatabaseDir { get; set; } = "./databases";
    [Description("插件配置目录")]
    public string PluginConfigurationDir { get; set; } = "./configurations";
    [Description("缓存图片目录")]
    public string CacheImageDir { get; set; } = "./caches/images";
    [Description("gifsicle插件位置")]
    public string GifSiclePath { get; set; }
    [Description("ffmpeg插件位置")]
    public string FfMpegPath { get; set; }
}