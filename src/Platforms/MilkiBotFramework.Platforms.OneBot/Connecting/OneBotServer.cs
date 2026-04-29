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
        return OneBotWebSocketHelper.SendMessageAsync<T>(
            (message, state) => SendMessageAsync(message, state, selfId),
            action,
            @params);
    }

    protected override bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        return OneBotWebSocketHelper.TryGetStateByMessage(this, msg, out state);
    }

    protected override bool AllowMultipleConnections => true;

    protected override string? ResolveConnectionAccountId(string message)
    {
        return OneBotWebSocketHelper.ResolveSelfId(message);
    }

    protected override string? ResolveConnectionAccountId(IDictionary<string, string>? headers)
    {
        if (headers == null)
        {
            return null;
        }

        return headers.TryGetValue("X-Self-ID", out var selfId)
            ? selfId
            : headers.FirstOrDefault(k =>
                string.Equals(k.Key, "X-Self-ID", StringComparison.OrdinalIgnoreCase)).Value;
    }
}
