using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApiConnector : AspnetcoreConnector
{
    private readonly LightHttpClient _httpClient;

    private readonly ILogger<QApiConnector> _logger;

    private DateTime _tokenExpireTime;
    private string? _accessToken;
    private int _lastSequence;
    private ConcurrentDictionary<string, DateTimeOffset> _cachedMessages = new();

    public QApiConnector(LightHttpClient httpClient, ILogger<QApiConnector> logger, WebApplication webApplication)
        : base(null, logger, webApplication)
    {
        _logger = logger;
        _httpClient = httpClient;
    }


    public QConnection Connection { get; internal set; }
    public override string BindingPath
    {
        get => Connection.CallbackPath;
        set => Connection.CallbackPath = value;
    }

    public string Host
    {
        get
        {
            if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));
            return QApiTokenHelper.GetHost(Connection);
        }
    }

    public int MessageSequence => _lastSequence;

    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));
        await UpdateAccessTokenAsync();
        await base.ConnectAsync(cancellationToken);
    }

    public async Task HandleEventAsync(OpCode opCode, JsonNode json)
    {
        var id = json["id"]?.GetValue<string>();
        if (id == null || _cachedMessages.TryAdd(id, DateTimeOffset.Now))
        {
            var jsonString = json.ToJsonString();
            _logger.LogDebug(jsonString);
            await PublishInboundMessageAsync(InboundMessage.FromRawText(jsonString, "qq-http"));
        }
    }

    public async ValueTask<string> GetAuthorizationAsync()
    {
        if (DateTime.Now >= _tokenExpireTime.AddSeconds(-60))
        {
            _logger.LogInformation("Token expired, refreshing..");
            await UpdateAccessTokenAsync();
        }

        return $"QQBot {_accessToken}";
    }

    private async Task UpdateAccessTokenAsync()
    {
        var (accessToken, expireTime) = await QApiTokenHelper.RequestAccessTokenAsync(_httpClient, Connection);
        _accessToken = accessToken;
        _tokenExpireTime = expireTime;
    }
}

internal static class QApiTokenHelper
{
    private const string ProductHost = "api.sgroup.qq.com";
    private const string SandboxHost = "sandbox.api.sgroup.qq.com";

    public static string GetHost(QConnection connection)
    {
        return connection.IsDevelopment ? SandboxHost : ProductHost;
    }

    public static async Task<(string AccessToken, DateTime ExpireTime)> RequestAccessTokenAsync(LightHttpClient httpClient, QConnection connection)
    {
        var response = await httpClient.HttpPost<string>(
            "https://bots.qq.com/app/getAppAccessToken",
            new
            {
                appId = connection.AppId,
                clientSecret = connection.ClientSecret
            });

        var jsonNode = JsonNode.Parse(response)!;
        ValidateResult(jsonNode);

        var accessToken = jsonNode["access_token"]!.GetValue<string>();
        var expiresIn = int.Parse(jsonNode["expires_in"]!.GetValue<string>());
        return (accessToken, DateTime.Now.AddSeconds(expiresIn));
    }

    public static void ValidateResult(JsonNode jsonNode)
    {
        var code = jsonNode["code"];
        if (code != null)
        {
            var message = jsonNode["message"];
            throw new QApiException(code.GetValue<int>().ToString(), message?.GetValue<string>());
        }
    }
}