namespace MilkiBotFramework.Platforms.GoCqHttp.Utils;

public static class EncodingHelper
{
    public static string EncodeFileToBase64(Stream stream)
    {
        if (stream.Position != 0) stream.Position = 0;

        var length = stream.Length;
        Span<byte> span = length <= FrameworkConstants.MaxStackArrayLength
            ? stackalloc byte[(int)length]
            : new byte[length];
        _ = stream.Read(span);
        return Convert.ToBase64String(span);
    }

    public static string EncodeFileToBase64(string path)
    {
        using var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Write);
        return EncodeFileToBase64(fileStream);
    }

    public static byte[] DecodeBase64ToBytes(string base64)
    {
        return Convert.FromBase64String(base64);
    }
}