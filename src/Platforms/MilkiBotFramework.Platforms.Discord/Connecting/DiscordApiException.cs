namespace MilkiBotFramework.Platforms.Discord.Connecting;

/// <summary>
///     表示 Discord API 调用过程中发生的异常。
/// </summary>
public class DiscordApiException : Exception
{
    public DiscordApiException(int errorCode, string message) : base($"Discord API Error {errorCode}: {message}")
    {
        ErrorCode = errorCode;
    }

    public DiscordApiException(int errorCode, string message, Exception innerException)
        : base($"Discord API Error {errorCode}: {message}", innerException)
    {
        ErrorCode = errorCode;
    }

    /// <summary>
    ///     Discord API 错误码。
    /// </summary>
    public int ErrorCode { get; }
}