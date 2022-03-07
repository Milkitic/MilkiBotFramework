using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
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
        if (ConnectionType == ConnectionType.ReverseWebsocket)
            return await GoCqWebsocketHelper.SendMessageAsync<T>(this, action, @params);
        if (WebSocketConnector == null)
            return await _lightHttpClient.HttpPost<GoCqApiResponse<T>>(TargetUri + "/" + action, @params);
        if (WebSocketConnector is IGoCqConnector goCqConnector)
            return await goCqConnector.SendMessageAsync<T>(action, @params);
        throw new ArgumentException(null, nameof(WebSocketConnector));
    }

    protected override bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        return GoCqWebsocketHelper.TryGetStateByMessage(this, msg, out state);
    }

    public GoCqKestrelConnector(IWebSocketConnector? webSocketConnector,
        ILogger<GoCqKestrelConnector> logger,
        LightHttpClient lightHttpClient,
        WebApplication webApplication)
        : base(webSocketConnector, logger, webApplication)
    {
        _lightHttpClient = lightHttpClient;
    }
}