using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Connecting;

public sealed class GoCqWsRequest
{
    [JsonPropertyName("echo")]
    public string State { get; set; }
    [JsonPropertyName("action")]
    public string Action { get; set; }
    [JsonPropertyName("params")]
    public IDictionary<string, object>? Params { get; set; }
}