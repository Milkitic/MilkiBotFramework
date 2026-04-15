using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting.RequestModel;
using MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel;

namespace MilkiBotFramework.Platforms.OneBot.Connecting;

internal static class OneBotWebSocketHelper
{
    public static async Task<OneBotApiResponse<T>> SendMessageAsync<T>(IConnector connector,
        string action,
        IDictionary<string, object>? @params)
    {
        var state = Guid.NewGuid().ToString("B");
        var req = new OneBotRequest
        {
            Action = action,
            Params = @params,
            State = state
        };
        var reqJson = JsonSerializer.Serialize(req);
        var str = await connector.SendMessageAsync(reqJson, state);
        return JsonSerializer.Deserialize<OneBotApiResponse<T>>(str)!;
    }

    public static bool TryGetStateByMessage(IConnector connector,
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