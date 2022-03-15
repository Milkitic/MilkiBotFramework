using System.ComponentModel;

namespace MilkiBotFramework.Connecting;

public sealed class LightHttpClientCreationOptions
{
    [Description("代理服务器地址")]
    public string? ProxyUrl { get; set; }

    [Description("默认超时")]
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(8);

    [Description("自动重试次数")]
    public int RetryCount { get; set; } = 3;
}