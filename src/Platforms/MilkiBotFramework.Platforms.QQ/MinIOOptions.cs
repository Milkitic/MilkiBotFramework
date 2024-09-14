namespace MilkiBotFramework.Platforms.QQ;

public class MinIOOptions
{
    public string Endpoint { get; set; } = "min.io";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public bool UseSSL { get; set; } = true;
    public string BucketName { get; set; } = "milkibotframework-qq";
}