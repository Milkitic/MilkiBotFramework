using System.ComponentModel;

namespace MilkiBotFramework.Platforms.QQ;

/// <summary>QQ 官方适配器的连接和图片存储配置。</summary>
// ReSharper disable once InconsistentNaming
public class QQBotOptions : BotOptions
{
    /// <summary>当前机器人的接入配置。</summary>
    [Description("QQ API设置")]
    public QConnection Connection { get; set; } = new();

    /// <summary>保留用于读取旧配置的 MinIO 设置。新配置使用 <see cref="OssOptions"/>。</summary>
    [Description("MinIO 设置")]
    // ReSharper disable once InconsistentNaming
    public MinIOOptions MinIOOptions { get; set; } = new();

    /// <summary>本地图片和内存图片用于 QQ 转存的对象存储设置。</summary>
    [Description("OSS 设置")]
    // ReSharper disable once InconsistentNaming
    public OssOptions OssOptions { get; set; } = new();
}
