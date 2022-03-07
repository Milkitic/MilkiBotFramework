using Microsoft.AspNetCore.Builder;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Connecting;
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
        if (WebSocketConnector == null)
            return await _lightHttpClient.HttpPost<GoCqApiResponse<T>>(TargetUri + "/" + action, @params);
        if (WebSocketConnector is IGoCqConnector goCqConnector)
            return await goCqConnector.SendMessageAsync<T>(action, @params);
        throw new ArgumentException(null, nameof(WebSocketConnector));
    }

    public GoCqKestrelConnector(
        IWebSocketConnector? webSocketConnector,
        LightHttpClient lightHttpClient,
        WebApplication webApplication)
        : base(webSocketConnector, webApplication)
    {
        _lightHttpClient = lightHttpClient;
    }
}