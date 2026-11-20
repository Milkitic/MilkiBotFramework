using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

/// <summary>托管 QQ 官方 Webhook，并为当前机器人缓存和更新访问凭证。</summary>
public class QApiConnector : AspnetcoreConnector, IPlatformConnector
{
    private static readonly TimeSpan CallbackRetention = TimeSpan.FromHours(24);
    private readonly LightHttpClient _httpClient;
    private readonly ILogger<QApiConnector> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _cachedMessages = new();
    private volatile AccessToken? _cachedToken;
    private long _nextCacheSweep;
    private int _lastSequence;

    /// <summary>创建托管 QQ 回调的连接器。</summary>
    /// <param name="httpClient">用于取得访问凭证的 HTTP 客户端。</param>
    /// <param name="logger">连接和回调日志。</param>
    /// <param name="webApplication">承载回调服务的应用。</param>
    public QApiConnector(LightHttpClient httpClient, ILogger<QApiConnector> logger, WebApplication webApplication)
        : base(null, logger, webApplication)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>框架中 QQ 官方平台的标识。</summary>
    public string PlatformId => PlatformIds.Qq;

    /// <summary>当前机器人的接入配置。</summary>
    public QConnection Connection { get; set; } = new();

    /// <summary>Webhook 路径。配置为完整回调 URL 时取其路径部分。</summary>
    public override string? BindingPath
    {
        get => Connection.CallbackPath is { } value && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https"
            ? PathString.FromUriComponent(uri).Value
            : Connection.CallbackPath;
        set => Connection.CallbackPath = value;
    }

    /// <summary>当前机器人使用的 QQ OpenAPI 主机名。</summary>
    public string Host => QApiTokenHelper.GetHost(Connection);

    /// <summary>最近收到的非空 QQ 事件序号。Webhook 未提供序号时保留当前值。</summary>
    public int MessageSequence => Volatile.Read(ref _lastSequence);

    /// <summary>注册使用当前 QQ 连接器配置的 Webhook 中间件。</summary>
    /// <param name="application">承载 QQ 回调的 Web 应用。</param>
    /// <returns>中间件已注册时返回 <see langword="true"/>。</returns>
    public override bool TryConfigureHttpCallback(WebApplication application)
    {
        application.UseMiddleware<QApiHttpMiddleware>(this);
        return true;
    }

    /// <summary>取得机器人访问凭证并启动 Webhook 宿主。</summary>
    /// <param name="cancellationToken">取消启动的令牌。</param>
    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await GetAuthorizationAsync(cancellationToken);
        await base.ConnectAsync(cancellationToken);
    }

    /// <summary>将已验证的 QQ 事件发布给框架。近期重复投递的事件会被合并。</summary>
    /// <param name="opCode">回调操作码。仅发布事件数据包。</param>
    /// <param name="json">QQ 的完整事件载荷。</param>
    public async Task HandleEventAsync(OpCode opCode, JsonNode json)
    {
        if (opCode != OpCode.Dispatch || json is not JsonObject payload) return;

        if (payload["s"] is JsonValue sequence && sequence.TryGetValue<int>(out var value))
        {
            Volatile.Write(ref _lastSequence, value);
        }

        var now = DateTimeOffset.UtcNow;
        CleanCallbackCache(now);
        var eventId = payload["id"]?.ToString();
        if (string.IsNullOrEmpty(eventId) &&
            (payload["t"]?.ToString() is "GROUP_AT_MESSAGE_CREATE" or "GROUP_MESSAGE_CREATE" or "C2C_MESSAGE_CREATE") &&
            payload["d"] is JsonObject data && data["id"] is JsonValue messageId)
        {
            eventId = messageId.ToString();
        }

        if (!string.IsNullOrEmpty(eventId) && !_cachedMessages.TryAdd(eventId, now)) return;

        _logger.LogDebug("Dispatching QQ callback {EventType}", payload["t"]?.ToString());
        await PublishInboundMessageAsync(InboundMessage.FromRawText(payload.ToJsonString(), "qq-http"));
    }

    private void CleanCallbackCache(DateTimeOffset now)
    {
        var nextSweep = Volatile.Read(ref _nextCacheSweep);
        if (now.Ticks < nextSweep ||
            Interlocked.CompareExchange(ref _nextCacheSweep, now.AddHours(1).Ticks, nextSweep) != nextSweep)
        {
            return;
        }

        foreach (var entry in _cachedMessages)
        {
            if (now - entry.Value >= CallbackRetention)
            {
                _cachedMessages.TryRemove(entry);
            }
        }
    }

    /// <summary>返回格式为 <c>QQBot &lt;access_token&gt;</c> 的 HTTP 鉴权值。</summary>
    /// <returns>可用于 <c>Authorization</c> 请求头的值。</returns>
    public ValueTask<string> GetAuthorizationAsync()
    {
        return GetAuthorizationAsync(CancellationToken.None);
    }

    internal async ValueTask<string> GetAuthorizationAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_cachedToken is { } cached && cached.ExpireTime > DateTimeOffset.UtcNow.AddMinutes(1))
        {
            return cached.Authorization;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedToken is { } current && current.ExpireTime > DateTimeOffset.UtcNow.AddMinutes(1))
            {
                return current.Authorization;
            }

            _logger.LogInformation("Refreshing QQ bot access token.");
            var (accessToken, expireTime) = await QApiTokenHelper.RequestAccessTokenAsync(_httpClient, Connection, cancellationToken);
            var refreshed = new AccessToken($"QQBot {accessToken}", expireTime);
            _cachedToken = refreshed;
            return refreshed.Authorization;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private sealed record AccessToken(string Authorization, DateTimeOffset ExpireTime);
}

internal static class QApiTokenHelper
{
    private const string ProductHost = "api.bot.qq.com";
    private const string SandboxHost = "sandbox.api.sgroup.qq.com";

    internal static string GetHost(QConnection connection)
    {
        return connection.IsDevelopment ? SandboxHost : ProductHost;
    }

    internal static async Task<(string AccessToken, DateTimeOffset ExpireTime)> RequestAccessTokenAsync(
        LightHttpClient httpClient, QConnection connection, CancellationToken cancellationToken = default)
    {
        var requestedAt = DateTimeOffset.UtcNow;
        using var response = await httpClient.SendOnceAsync(HttpMethod.Post,
            new Uri($"https://{ProductHost}/app/getAppAccessToken"),
            new
            {
                appId = connection.AppId,
                clientSecret = connection.ClientSecret
            }, cancellationToken: cancellationToken);

        var data = await QOpenApiResponse.ReadAsync(response, cancellationToken) as JsonObject;
        if (data?["access_token"] is not JsonValue token || !token.TryGetValue<string>(out var accessToken) ||
            string.IsNullOrWhiteSpace(accessToken) || !int.TryParse(data["expires_in"]?.ToString(), out var expiresIn) ||
            expiresIn <= 0)
        {
            throw new QApiException("QQ 访问凭证响应缺少有效的 access_token 或 expires_in", null, response.StatusCode);
        }

        return (accessToken, requestedAt.AddSeconds(expiresIn));
    }
}
