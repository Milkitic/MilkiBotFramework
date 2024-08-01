namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApiException : Exception
{
    public QApiException(string error, string? message) : base(message == null ? error : $"{error}: {message}")
    {
    }
}