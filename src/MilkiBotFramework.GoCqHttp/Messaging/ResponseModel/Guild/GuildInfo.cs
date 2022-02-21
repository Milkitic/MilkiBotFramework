using System;
using System.Text.Json.Serialization;
using MilkiBotFramework.GoCqHttp.Internal;

namespace MilkiBotFramework.GoCqHttp.Messaging.ResponseModel.Guild;

public class GuildInfo
{
    [JsonPropertyName("create_time")]
    [JsonConverter(typeof(UnixDateTimeConverter))]
    public DateTimeOffset CreateTime { get; set; }

    //[JsonPropertyName("guild_id")]
    //public long GuildId { get; set; }

    [JsonPropertyName("guild_name")]
    public string GuildName { get; set; }

    [JsonPropertyName("guild_profile")]
    public string GuildProfile { get; set; }

    [JsonPropertyName("max_admin_count")]
    public int MaxAdminCount { get; set; }

    [JsonPropertyName("max_member_count")]
    public int MaxMemberCount { get; set; }

    [JsonPropertyName("max_robot_count")]
    public int MaxRobotCount { get; set; }

    [JsonPropertyName("member_count")]
    public int MemberCount { get; set; }

    [JsonPropertyName("owner_id")]
    public long OwnerId { get; set; }
}