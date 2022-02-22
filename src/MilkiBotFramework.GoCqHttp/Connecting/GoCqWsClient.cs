using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.GoCqHttp.Connecting.RequestModel;
using MilkiBotFramework.GoCqHttp.Connecting.ResponseModel;

namespace MilkiBotFramework.GoCqHttp.Connecting;

public sealed class GoCqWsClient : WebSocketClientConnector, IGoCqConnector
{

    public GoCqWsClient(ILogger<GoCqWsClient> logger) : base(logger)
    {
    }

    public Task<GoCqWsResponse<object>> SendMessageAsync(string action, IDictionary<string, object>? @params)
    {
        return SendMessageAsync<object>(action, @params);
    }

    public async Task<GoCqWsResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params)
    {
        var state = Guid.NewGuid().ToString("B");
        var req = new GoCqWsRequest
        {
            Action = action,
            Params = @params,
            State = state
        };
        var reqJson = JsonSerializer.Serialize(req);
        var str = await base.SendMessageAsync(reqJson, state);
        return JsonSerializer.Deserialize<GoCqWsResponse<T>>(str)!;
    }

    protected override bool TryGetStateByMessage(string msg, [NotNullWhen(true)] out string? state)
    {
        var jDoc = JsonDocument.Parse(msg);
        var hasProperty = jDoc.RootElement.TryGetProperty("echo", out var echoElement);
        if (!hasProperty)
        {
            state = null;
            return false;
        }

        state = echoElement.GetString();
        return !string.IsNullOrEmpty(state);
    }
}