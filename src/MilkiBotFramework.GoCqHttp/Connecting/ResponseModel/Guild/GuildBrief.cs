using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Connecting.ResponseModel.Guild;

public class GuildBrief
{
    [JsonPropertyName("guild_display_id")]
    public long GuildDisplayId { get; set; }

    [JsonPropertyName("guild_id")]
    public long GuildId { get; set; }

    [JsonPropertyName("guild_name")]
    public string GuildName { get; set; }
}