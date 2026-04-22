using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.OneBot.Connecting;

public sealed class OneBotKestrelConnector : AspnetcoreConnector, IOneBotConnector, IPlatformConnector
{
    private readonly LightHttpClient _lightHttpClient;
    public string PlatformId => PlatformIds.OneBot;

    public Task<OneBotApiResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params, string selfId)
    {
        return SendMessageAsync<object>(action, @params, selfId);
    }

    public async Task<OneBotApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params, string selfId)
    {
        if (ConnectionType == ConnectionType.ReverseWebSocket)
            return await OneBotWebSocketHelper.SendMessageAsync<T>(
                (message, state) => SendMessageAsync(message, state, selfId),
                action,
                @params);
        if (WebSocketConnector == null)
            return await _lightHttpClient.HttpPost<OneBotApiResponse<T>>(TargetUri + "/" + action, @params);
        if (WebSocketConnector is IOneBotConnector oneBotConnector)
            return await oneBotConnector.SendMessageAsync<T>(action, @params, selfId);
        throw new ArgumentException(null, nameof(WebSocketConnector));
    }

    protected override bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        return OneBotWebSocketHelper.TryGetStateByMessage(this, msg, out state);
    }

    protected override bool AllowMultipleReverseWebSocketConnections => true;

    protected override string? ResolveReverseWebSocketAccountId(IHeaderDictionary? headers)
    {
        if (headers == null)
        {
            return null;
        }

        return headers.TryGetValue("X-Self-ID", out var selfIdValues)
            ? selfIdValues.ToString()
            : null;
    }

    protected override string? ResolveReverseWebSocketAccountId(string message)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(message);
            if (!jsonDocument.RootElement.TryGetProperty("self_id", out var selfIdElement))
            {
                return null;
            }

            return selfIdElement.ValueKind switch
            {
                JsonValueKind.String => selfIdElement.GetString(),
                JsonValueKind.Number => selfIdElement.GetInt64().ToString(),
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public OneBotKestrelConnector(ILogger<OneBotKestrelConnector> logger,
        LightHttpClient lightHttpClient,
        WebApplication webApplication,
        IWebSocketConnector? webSocketConnector = null)
        : base(webSocketConnector, logger, webApplication)
    {
        _lightHttpClient = lightHttpClient;
    }
}