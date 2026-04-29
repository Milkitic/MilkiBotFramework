using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.OneBot.Connecting;

public sealed class OneBotClient : WebSocketClientConnector, IOneBotConnector, IPlatformConnector
{
    public string PlatformId => PlatformIds.OneBot;

    public OneBotClient(ILogger<OneBotClient> logger) : base(logger)
    {
    }

    public Task<OneBotApiResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params, string selfId)
    {
        return SendMessageAsync<object>(action, @params, selfId);
    }

    public Task<OneBotApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params, string selfId)
    {
        // WebSocket client mode owns a single outbound connection, so selfId is accepted only
        // to satisfy the multi-account connector contract.
        return OneBotWebSocketHelper.SendMessageAsync<T>(this, action, @params);
    }

    protected override bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        return OneBotWebSocketHelper.TryGetStateByMessage(this, msg, out state);
    }
}
