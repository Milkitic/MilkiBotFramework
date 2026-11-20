using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

internal static class QOpenApiResponse
{
    internal static async Task<JsonNode?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var headerTraceId = response.Headers.TryGetValues("X-Tps-trace-ID", out var values)
            ? values.FirstOrDefault()
            : null;
        return Read(body, response.StatusCode, headerTraceId, response.ReasonPhrase);
    }

    internal static JsonNode? Read(string body, HttpStatusCode statusCode, string? headerTraceId = null,
        string? reasonPhrase = null)
    {
        var success = (int)statusCode is >= 200 and < 300;
        JsonNode? result = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                result = JsonNode.Parse(body);
            }
        }
        catch (JsonException exception) when (success)
        {
            throw new QApiException("QQ API 响应格式无效", null, statusCode, headerTraceId, exception);
        }
        catch (JsonException)
        {
            // 网关或代理也可能返回非 JSON 错误页面。
        }

        var data = result as JsonObject;
        var code = data?["err_code"]?.ToString() ?? data?["code"]?.ToString();
        var isPendingAudit = (int)statusCode is 201 or 202 && code is "304023" or "304024";
        if (!success || (code is not (null or "0") && !isPendingAudit))
        {
            var traceId = data?["trace_id"]?.ToString() ?? headerTraceId;
            var message = data?["message"]?.ToString() ?? reasonPhrase;
            throw new QApiException(code ?? $"HTTP {(int)statusCode}", message, statusCode, traceId);
        }

        return result;
    }
}
