using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting;

public sealed class GoCqClient : WebSocketClientConnector, IGoCqConnector
{
    public GoCqClient(ILogger<GoCqClient> logger) : base(logger)
    {
    }

    public Task<GoCqApiResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params)
    {
        return SendMessageAsync<object>(action, @params);
    }

    public Task<GoCqApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params)
    {
        return GoCqWebSocketHelper.SendMessageAsync<T>(this, action, @params);
    }

    protected override bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        return GoCqWebSocketHelper.TryGetStateByMessage(this, msg, out state);
    }
}