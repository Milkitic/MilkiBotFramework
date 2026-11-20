namespace MilkiBotFramework.Platforms.QQ;

/// <summary>保留用于读取旧配置的 MinIO 选项。</summary>
[Obsolete("已无作用，仅作为配置保留，请使用OssOptions")]
// ReSharper disable once InconsistentNaming
public class MinIOOptions
{
    /// <summary>旧配置中的 MinIO 主机名。</summary>
    public string Endpoint { get; set; } = "min.io";
    /// <summary>旧配置中的访问密钥标识。</summary>
    public string AccessKey { get; set; } = "";
    /// <summary>旧配置中的访问密钥。</summary>
    public string SecretKey { get; set; } = "";

    /// <summary>旧配置中的 HTTPS 开关。</summary>
    // ReSharper disable once InconsistentNaming
    public bool UseSSL { get; set; } = true;
    /// <summary>旧配置中的存储桶名称。</summary>
    public string BucketName { get; set; } = "milkibotframework-qq";
}
