using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApiConnector : WebSocketClientConnector
{
    private const string ProductHost = "api.sgroup.qq.com";
    private const string SandboxHost = "sandbox.api.sgroup.qq.com";

    private readonly LightHttpClient _httpClient;

    private DateTime _tokenExpireTime;
    private string? _accessToken;

    private string? _wsUrl;

    public QApiConnector(LightHttpClient httpClient, ILogger<WebSocketClientConnector> logger) : base(logger)
    {
        _httpClient = httpClient;
        RawMessageReceived += QApiConnector_RawMessageReceived;
    }

    private async Task QApiConnector_RawMessageReceived(string message)
    {
    }

    public QConnection? Connection { get; set; }

    public string Host
    {
        get
        {
            if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));
            return Connection.IsDevelopment ? SandboxHost : ProductHost;
        }
    }

    public override async Task ConnectAsync()
    {
        if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));
        await RequestAccessToken(Connection);
        await RequestEndpoint();

        TargetUri = _wsUrl;

        //var s = JsonSerializer.Deserialize<resp_getAppAccessToken>(response);
        await base.ConnectAsync();
        var s = JsonSerializer.Serialize(new
        {
            op = 2,
            d = new
            {
                token = $"QQBot {_accessToken}",
                intents = 1845498883,
                shard = new[] { 0, 1 },
            }
        });
        await SendMessageAsync(s, null);
    }

    private async Task RequestAccessToken(QConnection connection)
    {
        var response = await _httpClient.HttpPost<string>(
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
        _accessToken = accessToken;
        _tokenExpireTime = DateTime.Now.AddSeconds(expiresIn);
    }

    private async Task RequestEndpoint()
    {
        var response2 = await _httpClient.HttpGet<string>(
            $"https://{Host}/gateway", headers: new Dictionary<string, string>()
            {
                ["Authorization"] = "QQBot " + _accessToken
            });
        var jsonNode2 = JsonNode.Parse(response2)!;
        ValidateResult(jsonNode2);
        _wsUrl = jsonNode2["url"]!.GetValue<string>();
    }

    private static void ValidateResult(JsonNode jsonNode)
    {
        var code = jsonNode["code"];
        if (code != null)
        {
            var message = jsonNode["message"];
            throw new QApiException(code.GetValue<int>().ToString(), message?.GetValue<string>());
        }
    }
    //public Task ConnectAsync()
    //{
    //    if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));

    //    throw new NotImplementedException();
    //}

    //public Task DisconnectAsync()
    //{
    //    throw new NotImplementedException();
    //}

    //public Task<string> SendMessageAsync(string message, string state)
    //{
    //    throw new NotImplementedException();
    //}
}