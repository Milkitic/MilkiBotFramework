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
        JsonNode? result = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                result = JsonNode.Parse(body);
            }
        }
        catch (JsonException exception) when (response.IsSuccessStatusCode)
        {
            throw new QApiException("QQ API 响应格式无效", null, response.StatusCode, headerTraceId, exception);
        }
        catch (JsonException)
        {
            // 网关或代理也可能返回非 JSON 错误页面。
        }

        var data = result as JsonObject;
        var code = data?["err_code"]?.ToString() ?? data?["code"]?.ToString();
        var isPendingAudit = (int)response.StatusCode is 201 or 202 && code is "304023" or "304024";
        if (!response.IsSuccessStatusCode || (code is not (null or "0") && !isPendingAudit))
        {
            var traceId = data?["trace_id"]?.ToString() ?? headerTraceId;
            var message = data?["message"]?.ToString() ?? response.ReasonPhrase;
            throw new QApiException(code ?? $"HTTP {(int)response.StatusCode}", message, response.StatusCode, traceId);
        }

        return result;
    }
}
