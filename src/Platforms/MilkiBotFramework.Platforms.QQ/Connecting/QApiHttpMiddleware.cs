using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using NSec.Cryptography;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

/// <summary>处理 QQ 官方 Webhook 的地址验证、事件验签和回执。</summary>
public class QApiHttpMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<QApiHttpMiddleware> _logger;
    private readonly QApiConnector _connector;

    /// <summary>创建使用 QQ 连接器配置的回调中间件。</summary>
    /// <param name="next">处理非 QQ 回调的后续中间件。</param>
    /// <param name="botOptions">保留的框架配置参数。Webhook 凭证取自连接器的配置。</param>
    /// <param name="connector">对应 QQ 机器人的连接器。</param>
    /// <param name="logger">用于记录回调失败的日志对象。</param>
    public QApiHttpMiddleware(RequestDelegate next, BotOptions botOptions,
        IConnector connector, ILogger<QApiHttpMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _connector = (QApiConnector)connector;
    }

    /// <summary>处理配置路径上的 QQ 回调。其他路径交给应用的后续中间件。</summary>
    /// <param name="context">当前 HTTP 请求及响应。</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = _connector.BindingPath;
        if (string.IsNullOrWhiteSpace(path) || !context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!HttpMethods.IsPost(context.Request.Method))
        {
            context.Response.Headers.Allow = HttpMethods.Post;
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        using var buffer = new MemoryStream();
        await context.Request.Body.CopyToAsync(buffer, context.RequestAborted);
        var body = buffer.GetBuffer().AsMemory(0, (int)buffer.Length);
        JsonObject? payload;
        try
        {
            payload = JsonNode.Parse(body.Span) as JsonObject;
        }
        catch (JsonException)
        {
            payload = null;
        }

        if (payload?["op"] is not JsonValue op || !op.TryGetValue<int>(out var opCode))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var secret = _connector.Connection.ClientSecret;
        var appId = context.Request.Headers["X-Bot-Appid"].ToString();
        if (string.IsNullOrEmpty(secret) || (appId.Length != 0 && appId != _connector.Connection.AppId))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (opCode == (int)OpCode.Validate)
        {
            await ReplyToValidationAsync(context, payload["d"] as JsonObject, secret);
            return;
        }

        var signature = context.Request.Headers["X-Signature-Ed25519"].ToString();
        var timestamp = context.Request.Headers["X-Signature-Timestamp"].ToString();
        if (!VerifySignature(secret, timestamp, signature, body.Span))
        {
            _logger.LogWarning("QQ Webhook 请求签名无效。");
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (opCode == (int)OpCode.Dispatch &&
            (payload["t"] is not JsonValue eventType || !eventType.TryGetValue<string>(out var type) ||
             string.IsNullOrWhiteSpace(type) || payload["d"] is not JsonObject))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        await context.Response.WriteAsJsonAsync(new { op = (int)OpCode.HTTPCallbackACK, d = 0 }, context.RequestAborted);
        await context.Response.CompleteAsync();

        if (opCode == (int)OpCode.Dispatch)
        {
            try
            {
                await _connector.HandleEventAsync(OpCode.Dispatch, payload);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "处理已接收的 QQ 回调时失败。");
            }
        }
    }

    private static async Task ReplyToValidationAsync(HttpContext context, JsonObject? data, string secret)
    {
        var timestamp = data?["event_ts"]?.ToString();
        if (data?["plain_token"] is not JsonValue token || !token.TryGetValue<string>(out var plainToken) ||
            string.IsNullOrWhiteSpace(plainToken) || !IsTimestamp(timestamp))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // 地址验证只签发平台的普通挑战令牌，避免签发可作为事件正文的 JSON 数据。
        if (plainToken.TrimStart().StartsWith('{'))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var key = CreateKey(secret);
        var signature = SignatureAlgorithm.Ed25519.Sign(key, Encoding.UTF8.GetBytes(timestamp + plainToken));
        await context.Response.WriteAsJsonAsync(new { plain_token = plainToken, signature = Convert.ToHexStringLower(signature) },
            context.RequestAborted);
    }

    private static bool VerifySignature(string secret, string timestamp, string signature, ReadOnlySpan<byte> body)
    {
        if (!IsTimestamp(timestamp) || signature.Length != SignatureAlgorithm.Ed25519.SignatureSize * 2)
        {
            return false;
        }

        Span<byte> signatureBytes = stackalloc byte[SignatureAlgorithm.Ed25519.SignatureSize];
        if (Convert.FromHexString(signature, signatureBytes, out _, out _) != System.Buffers.OperationStatus.Done)
        {
            return false;
        }

        var signedData = new byte[timestamp.Length + body.Length];
        Encoding.UTF8.GetBytes(timestamp, signedData);
        body.CopyTo(signedData.AsSpan(timestamp.Length));
        using var key = CreateKey(secret);
        return SignatureAlgorithm.Ed25519.Verify(key.PublicKey, signedData, signatureBytes);
    }

    private static bool IsTimestamp(string? value) => !string.IsNullOrEmpty(value) && value.All(char.IsAsciiDigit);

    private static Key CreateKey(string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        Span<byte> seed = stackalloc byte[32];
        for (var i = 0; i < seed.Length; i++)
        {
            seed[i] = secretBytes[i % secretBytes.Length];
        }

        return Key.Import(SignatureAlgorithm.Ed25519, seed, KeyBlobFormat.RawPrivateKey);
    }
}
