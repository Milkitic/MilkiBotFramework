using System.ComponentModel;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Plugining.Configuration;

namespace MilkiBotFramework;

public class BotOptions : ConfigurationBase
{
    private LightHttpClientCreationOptions? _httpOptions;

    public LightHttpClientCreationOptions HttpOptions
    {
        get => _httpOptions ?? new LightHttpClientCreationOptions();
        set => _httpOptions = value;
    }

    [Description("自定义变量，支持在插件的 [DescriptionAttribute] 和 IResponse(Text) 中进行替换")]
    public Dictionary<string, string> Variables { get; set; } = new()
    {
        ["BotCode"] = "MilkiBot",
        ["BotNick"] = "MilkiBot",
    };

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