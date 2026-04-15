using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.OneBot.Connecting;

public sealed class OneBotClient : WebSocketClientConnector, IOneBotConnector
{
    public OneBotClient(ILogger<OneBotClient> logger) : base(logger)
    {
    }

    public Task<OneBotApiResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params)
    {
        return SendMessageAsync<object>(action, @params);
    }

    public Task<OneBotApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params)
    {
        return OneBotWebSocketHelper.SendMessageAsync<T>(this, action, @params);
    }

    protected override bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        return OneBotWebSocketHelper.TryGetStateByMessage(this, msg, out state);
    }
}