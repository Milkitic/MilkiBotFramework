using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.RequestModel;
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

    public async Task<GoCqApiResponse<T>> SendMessageAsync<T>(string action, IDictionary<string, object>? @params)
    {
        var state = Guid.NewGuid().ToString("B");
        var req = new GoCqRequest
        {
            Action = action,
            Params = @params,
            State = state
        };
        var reqJson = JsonSerializer.Serialize(req);
        var str = await base.SendMessageAsync(reqJson, state);
        return JsonSerializer.Deserialize<GoCqApiResponse<T>>(str)!;
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