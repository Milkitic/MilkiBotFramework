#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.OneBot.Connecting.RequestModel;

public sealed class OneBotRequest
{
    [JsonPropertyName("echo")]
    public string State { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; }
 
    [JsonPropertyName("params")]
    public IDictionary<string, object> Params { get; set; }
}