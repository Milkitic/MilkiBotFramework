using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace MilkiBotFramework.Platforms.QQ;

public class QApiHttpMiddleware
{
    private readonly RequestDelegate _next;
    private readonly QQBotOptions _options;

    public QApiHttpMiddleware(RequestDelegate next, IOptions<QQBotOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            // 启用缓冲以允许多次读取请求体
            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true);
            var bodyStr = await reader.ReadToEndAsync();

            // 重置流位置以供后续中间件使用
            context.Request.Body.Position = 0;

            var xSignature = context.Request.Headers["X-Signature-Ed25519"].FirstOrDefault();
            var xTimestamp = context.Request.Headers["X-Signature-Timestamp"].FirstOrDefault();

            // 验证签名
            if (!VerifySignature(xSignature, xTimestamp, bodyStr))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized: Invalid signature");
                return;
            }
        }

        await _next(context);
    }

    private bool VerifySignature(string? signature, string? timestamp, string body)
    {
        if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(_options.Connection.BotSecret))
        {
            return false;
        }

        try
        {
            // 1. 根据Bot Secret生成公钥
            var publicKey = GeneratePublicKey(_options.Connection.ClientSecret);

            // 2. 解码签名
            var sig = Convert.FromHexString(signature);
            if (sig.Length != 64 || (sig[63] & 224) != 0)
            {
                return false;
            }

            // 3. 构建签名体 (timestamp + body)
            var message = Encoding.UTF8.GetBytes(timestamp + body);

            // 4. 验证签名
            return Ed25519.Verify(publicKey, message, sig);
        }
        catch
        {
            return false;
        }
    }

    private static byte[] GeneratePublicKey(string? botSecret)
    {
        ArgumentNullException.ThrowIfNull(botSecret);

        // 根据botSecret进行repeat操作后得到seed值计算出公钥
        var seed = botSecret;
        while (seed.Length < 32)
        {
            seed += botSecret;
        }

        var seedBytes = Encoding.UTF8.GetBytes(seed[..32]);

        // 使用Ed25519算法生成密钥对
        var keyPair = Ed25519.GenerateKeyPair(seedBytes);
        return keyPair.PublicKey;
    }
}