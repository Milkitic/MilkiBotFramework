using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApiConnector : WebSocketClientConnector
{
    private readonly LightHttpClient _httpClient;

    public QApiConnector(LightHttpClient httpClient, ILogger<WebSocketClientConnector> logger) : base(logger)
    {
        _httpClient = httpClient;
    }

    public QConnection? Connection { get; set; }

    public override async Task ConnectAsync()
    {
        if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));
        var response = await _httpClient.HttpPost<string>(
            "https://bots.qq.com/app/getAppAccessToken",
            new
            {
                appId = Connection.AppId,
                clientSecret = Connection.ClientSecret
            });

        var jsonNode = JsonNode.Parse(response)!;
        var code = jsonNode["code"];
        var message = jsonNode["message"];
        if (code != null)
        {
            throw new QApiException(code.GetValue<int>().ToString(), message?.GetValue<string>());
        }

        //var s = JsonSerializer.Deserialize<resp_getAppAccessToken>(response);
        await base.ConnectAsync();
    }
    //public Task ConnectAsync()
    //{
    //    if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));

    //    throw new NotImplementedException();
    //}

    public Task DisconnectAsync()
    {
        throw new NotImplementedException();
    }

    public Task<string> SendMessageAsync(string message, string state)
    {
        throw new NotImplementedException();
    }
}