using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.RequestModel;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting;

public sealed class GoCqKestrelConnector : AspnetcoreConnector, IGoCqConnector
{
    private readonly LightHttpClient _lightHttpClient;

    public Task<GoCqApiResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params)
    {
        return SendMessageAsync<object>(action, @params);
    }

    public async Task<GoCqApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params)
    {
        if (WebSocketClientConnector != null)
        {
            var state = Guid.NewGuid().ToString("B");
            var req = new GoCqRequest
            {
                Action = action,
                Params = @params,
                State = state
            };
            var reqJson = JsonSerializer.Serialize(req);
            var str = await WebSocketClientConnector.SendMessageAsync(reqJson, state);
            return JsonSerializer.Deserialize<GoCqApiResponse<T>>(str);
        }

        return await _lightHttpClient.HttpPost<GoCqApiResponse<T>>(TargetUri + "/" + action, @params);
    }

    public GoCqKestrelConnector(LightHttpClient lightHttpClient,
        WebApplication webApplication,
        WebSocketClientConnector? webSocketClientConnector)
        : base(webApplication, webSocketClientConnector)
    {
        _lightHttpClient = lightHttpClient;
    }
}