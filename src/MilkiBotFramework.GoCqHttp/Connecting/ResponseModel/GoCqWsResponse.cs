using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Connecting.ResponseModel;

public sealed class GoCqWsResponse<T>
{
    [JsonPropertyName("echo")]
    public string State { get; set; }
    [JsonPropertyName("retcode")]
    public int RetCode { get; set; }
    [JsonPropertyName("status")]
    public string Status { get; set; }
    [JsonPropertyName("data")]
    public T Data { get; set; }
    [JsonPropertyName("msg")]
    public string Msg { get; set; }
    [JsonPropertyName("wording")]
    public string Wording { get; set; }
}