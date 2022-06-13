#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.RequestModel;

public sealed class GoCqRequest
{
    [JsonPropertyName("echo")]
    public string State { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; }
 
    [JsonPropertyName("params")]
    public IDictionary<string, object> Params { get; set; }
}