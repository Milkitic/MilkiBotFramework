using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.OneBot.Connecting;

public sealed class OneBotServer : WebSocketServerConnector, IOneBotConnector, IPlatformConnector
{
    public string PlatformId => PlatformIds.OneBot;

    public OneBotServer(ILogger<OneBotServer> logger) : base(logger)
    {
    }

    public Task<OneBotApiResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params, string selfId)
    {
        return SendMessageAsync<object>(action, @params, selfId);
    }

    public Task<OneBotApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params, string selfId)
    {
        return OneBotWebSocketHelper.SendMessageAsync<T>(this, action, @params);
    }

    protected override bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        return OneBotWebSocketHelper.TryGetStateByMessage(this, msg, out state);
    }
}