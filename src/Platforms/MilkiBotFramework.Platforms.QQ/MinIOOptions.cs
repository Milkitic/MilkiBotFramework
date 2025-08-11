namespace MilkiBotFramework.Platforms.QQ;

[Obsolete("已无作用，仅作为配置保留，请使用OssOptions")]
// ReSharper disable once InconsistentNaming
public class MinIOOptions
{
    public string Endpoint { get; set; } = "min.io";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";

    // ReSharper disable once InconsistentNaming
    public bool UseSSL { get; set; } = true;
    public string BucketName { get; set; } = "milkibotframework-qq";
}