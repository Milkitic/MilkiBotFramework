using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using MilkiBotFramework.Platforms.QQ.Connecting;
using MilkiBotFramework.Plugining.Configuration;
using NSec.Cryptography;

namespace MilkiBotFramework.Platforms.QQ;

public class QApiHttpMiddleware
{
    private readonly RequestDelegate _next;
    private readonly QConnection _connection;

    public QApiHttpMiddleware(RequestDelegate next, BotOptions botOptions)
    {
        _next = next;
        _connection = ((QQBotOptions)botOptions).Connection;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            // 启用缓冲以允许多次读取请求体
            context.Request.EnableBuffering();

            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true);
            var bodyStr = await reader.ReadToEndAsync();
            var json = JsonNode.Parse(bodyStr);

            var plainToken = json?["d"]?["plain_token"]?.GetValue<string>() ?? bodyStr;
            var eventTimespan = json?["d"]?["event_ts"]?.GetValue<string>();
            // 重置流位置以供后续中间件使用
            context.Request.Body.Position = 0;

            var xSignature = context.Request.Headers["X-Signature-Ed25519"].FirstOrDefault();
            var xTimestamp = eventTimespan ?? context.Request.Headers["X-Signature-Timestamp"].FirstOrDefault();

            // 验证签名
            if (!VerifySignature(xSignature, xTimestamp, bodyStr))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("Unauthorized: Invalid signature");
                return;
            }
            else
            {
                // 生成签名
                var generatedSignature = GenerateSignature(eventTimespan, plainToken);
                
                await context.Response.WriteAsJsonAsync(new
                {
                    plain_token = plainToken,
                    signature = generatedSignature
                });
                return;
            }
        }

        await _next(context);
    }

    private bool VerifySignature(string? signature, string? timestamp, string body)
    {
        if (string.IsNullOrEmpty(signature) ||
            string.IsNullOrEmpty(timestamp) ||
            string.IsNullOrEmpty(_connection.ClientSecret))
        {
            return false;
        }

        try
        {
            // 1. 根据Bot Secret生成公钥
            var publicKey = GeneratePublicKey(_connection.ClientSecret);

            // 2. 解码签名
            var sig = Convert.FromHexString(signature);
            if (sig.Length != SignatureAlgorithm.Ed25519.SignatureSize || (sig[63] & 224) != 0)
            {
                return false;
            }

            // 3. 构建签名体 (timestamp + body)
            var message = Encoding.UTF8.GetBytes(timestamp + body);

            // 4. 验证签名
            return SignatureAlgorithm.Ed25519.Verify(publicKey, message, sig);
        }
        catch
        {
            return false;
        }
    }

    private string GenerateSignature(string? timestamp, string content)
    {
        if (string.IsNullOrEmpty(timestamp) || string.IsNullOrEmpty(_connection.ClientSecret))
        {
            return string.Empty;
        }

        try
        {
            // 1. 根据Bot Secret生成私钥
            var privateKey = GeneratePrivateKey(_connection.ClientSecret);

            // 2. 构建签名体 (timestamp + content)
            var message = Encoding.UTF8.GetBytes(timestamp + content);

            // 3. 生成Ed25519签名
            var signature = SignatureAlgorithm.Ed25519.Sign(privateKey, message);

            // 4. 转换为十六进制字符串
            return Convert.ToHexString(signature).ToLower();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static Key GeneratePrivateKey(string botSecret)
    {
        var seed = botSecret;
        const int Ed25519SeedSize = 32;
        
        while (seed.Length < Ed25519SeedSize)
        {
            seed += seed; 
        }
        
        var seedString = seed.Substring(0, Ed25519SeedSize);
        byte[] seedBytes = Encoding.UTF8.GetBytes(seedString);

        return Key.Import(SignatureAlgorithm.Ed25519, seedBytes, KeyBlobFormat.RawPrivateKey);
    }

    private static PublicKey GeneratePublicKey(string botSecret)
    {
        var seed = botSecret;
        const int Ed25519SeedSize = 32;

        while (seed.Length < Ed25519SeedSize)
        {
            seed += seed; 
        }

        var seedBytes = Encoding.UTF8.GetBytes(seed[..32]);

        using var key = Key.Import(SignatureAlgorithm.Ed25519, seedBytes, KeyBlobFormat.RawPrivateKey);
        return key.PublicKey;
    }
}