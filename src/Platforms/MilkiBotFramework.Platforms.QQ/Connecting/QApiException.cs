using System.Net;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

/// <summary>表示 QQ 官方接口拒绝请求，或响应无法按接口契约读取。</summary>
public class QApiException : Exception
{
    /// <summary>使用 QQ 返回的错误码和说明创建异常。</summary>
    /// <param name="error">QQ 错误码或未取得业务码时的错误描述。</param>
    /// <param name="message">QQ 返回的诊断信息。</param>
    public QApiException(string error, string? message) : this(error, message, null)
    {
    }

    internal QApiException(string error, string? message, HttpStatusCode? statusCode, string? traceId = null,
        Exception? innerException = null)
        : base(message == null ? error : $"{error}: {message}", innerException)
    {
        Code = int.TryParse(error, out var code) ? code : null;
        StatusCode = statusCode;
        TraceId = traceId;
    }

    /// <summary>QQ 业务错误码。响应中没有数字错误码时为空。</summary>
    public int? Code { get; }
    /// <summary>QQ 接口返回的 HTTP 状态码。未收到 HTTP 响应时为空。</summary>
    public HttpStatusCode? StatusCode { get; }
    /// <summary>腾讯侧链路追踪 ID，供定位请求失败原因。</summary>
    public string? TraceId { get; }
}
