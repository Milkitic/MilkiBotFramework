namespace MilkiBotFramework.Platforms.QQ;

/// <summary>用于向 QQ 提供图片下载链接的对象存储配置。</summary>
// ReSharper disable once InconsistentNaming
public class OssOptions
{
    /// <summary>使用的对象存储服务商。</summary>
    public OssType OssType { get; set; } = OssType.Qiniu;
    /// <summary>对象存储的访问密钥标识。</summary>
    public string AccessKey { get; set; } = "";
    /// <summary>对象存储的私有访问密钥。</summary>
    public string SecretKey { get; set; } = "";
    /// <summary>用于存放待发送图片的存储桶名称。</summary>
    public string BucketName { get; set; } = "milkibotframework-qq";

    /// <summary>存储服务的主机或用于签发下载链接的自定义域名。</summary>
    public string CustomEndpoint { get; set; } = "min.io";
    /// <summary>是否使用 HTTPS 访问对象存储或生成下载链接。</summary>
    // ReSharper disable once InconsistentNaming
    public bool CustomUseSSL { get; set; } = true;
}

/// <summary>QQ 图片转存支持的对象存储服务商。</summary>
public enum OssType
{
    /// <summary>使用 MinIO 或兼容的对象存储。</summary>
    MinIO,
    /// <summary>使用七牛云对象存储。</summary>
    Qiniu
}
