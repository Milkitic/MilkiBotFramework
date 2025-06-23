using System.ComponentModel;

namespace MilkiBotFramework.Platforms.QQ;

public class QConnection
{
    [Description("是否为沙箱环境")]
    public bool IsDevelopment { get; set; }
    
    [Description("应用ID")]
    public string? AppId { get; set; }
    
    [Description("Bot密钥")]
    public string? ClientSecret { get; set; }
    
    [Description("回调路径")]
    public string? CallbackPath { get; set; }
}