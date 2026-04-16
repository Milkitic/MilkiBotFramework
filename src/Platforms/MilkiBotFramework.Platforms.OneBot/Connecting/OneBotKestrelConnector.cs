using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.OneBot.Connecting;

public sealed class OneBotKestrelConnector : AspnetcoreConnector, IOneBotConnector
{
    private readonly LightHttpClient _lightHttpClient;

    public Task<OneBotApiResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params)
    {
        return SendMessageAsync<object>(action, @params);
    }

    public async Task<OneBotApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params)
    {
        if (ConnectionType == ConnectionType.ReverseWebSocket)
            return await OneBotWebSocketHelper.SendMessageAsync<T>(this, action, @params);
        if (WebSocketConnector == null)
            return await _lightHttpClient.HttpPost<OneBotApiResponse<T>>(TargetUri + "/" + action, @params);
        if (WebSocketConnector is IOneBotConnector oneBotConnector)
            return await oneBotConnector.SendMessageAsync<T>(action, @params);
        throw new ArgumentException(null, nameof(WebSocketConnector));
    }

    protected override bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        return OneBotWebSocketHelper.TryGetStateByMessage(this, msg, out state);
    }

    public OneBotKestrelConnector(IWebSocketConnector? webSocketConnector,
        ILogger<OneBotKestrelConnector> logger,
        LightHttpClient lightHttpClient,
        WebApplication webApplication)
        : base(webSocketConnector, logger, webApplication)
    {
        _lightHttpClient = lightHttpClient;
    }
}