using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel.Guild;

public class GuildMember
{
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; }

    [JsonPropertyName("role")]
    public int Role { get; set; }

    [JsonPropertyName("tiny_id")]
    public long TinyId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; }
}