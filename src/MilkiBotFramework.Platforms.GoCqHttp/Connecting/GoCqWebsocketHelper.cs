using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.RequestModel;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting;

internal static class GoCqWebsocketHelper
{
    public static async Task<GoCqApiResponse<T>> SendMessageAsync<T>(IWebSocketConnector wsConnector,
        string action,
        IDictionary<string, object>? @params)
    {
        var state = Guid.NewGuid().ToString("B");
        var req = new GoCqRequest
        {
            Action = action,
            Params = @params,
            State = state
        };
        var reqJson = JsonSerializer.Serialize(req);
        var str = await wsConnector.SendMessageAsync(reqJson, state);
        return JsonSerializer.Deserialize<GoCqApiResponse<T>>(str)!;
    }

    public static bool TryGetStateByMessage(IWebSocketConnector wsConnector,
        string msg,
        [NotNullWhen(true)] out string? state)
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