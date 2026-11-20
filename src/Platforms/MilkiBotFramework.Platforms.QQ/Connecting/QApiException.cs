using System.Net;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApiException : Exception
{
    public QApiException(string error, string? message) : this(error, message, null)
    {
    }

    internal QApiException(string error, string? message, HttpStatusCode? statusCode)
        : base(message == null ? error : $"{error}: {message}")
    {
        Code = int.TryParse(error, out var code) ? code : null;
        StatusCode = statusCode;
    }

    public int? Code { get; }
    public HttpStatusCode? StatusCode { get; }
}
