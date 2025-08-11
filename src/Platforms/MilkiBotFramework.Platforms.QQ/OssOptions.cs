namespace MilkiBotFramework.Platforms.QQ;

// ReSharper disable once InconsistentNaming
public class OssOptions
{
    public OssType OssType { get; set; } = OssType.Qiniu;
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string BucketName { get; set; } = "milkibotframework-qq";

    public string CustomEndpoint { get; set; } = "min.io";
    // ReSharper disable once InconsistentNaming
    public bool CustomUseSSL { get; set; } = true;
}

public enum OssType
{
    MinIO, Qiniu
}