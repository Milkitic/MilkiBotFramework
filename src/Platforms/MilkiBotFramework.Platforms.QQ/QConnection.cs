using System.ComponentModel;

namespace MilkiBotFramework.Platforms.QQ;

/// <summary>QQ 机器人的鉴权和事件回调连接配置。</summary>
public class QConnection
{
    /// <summary>是否调用 QQ 沙箱环境的 OpenAPI。</summary>
    [Description("是否为沙箱环境")]
    public bool IsDevelopment { get; set; }
    
    /// <summary>在 QQ 开放平台取得的机器人 AppID。</summary>
    [Description("应用ID")]
    public string? AppId { get; set; }
    
    /// <summary>机器人的 AppSecret，用于换取访问凭证和验证事件回调。</summary>
    [Description("Bot密钥")]
    public string? ClientSecret { get; set; }
    
    /// <summary>配置的 Webhook 路径或完整 HTTPS URL。</summary>
    [Description("回调路径")]
    public string? CallbackPath { get; set; }
}
