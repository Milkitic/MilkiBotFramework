using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MilkiBotFramework.GoCqHttp.Internal;

namespace MilkiBotFramework.GoCqHttp.Messaging.ResponseModel.Guild;

public class SubChannelInfo
{
    [JsonPropertyName("channel_id")]
    public long ChannelId { get; set; }

    [JsonPropertyName("channel_name")]
    public string ChannelName { get; set; }

    [JsonPropertyName("channel_type")]
    public ChannelType ChannelType { get; set; }

    [JsonPropertyName("create_time")]
    [JsonConverter(typeof(UnixDateTimeConverter))]
    public DateTimeOffset CreateTime { get; set; }

    [JsonPropertyName("creator_id")]
    public long CreatorId { get; set; }

    [JsonPropertyName("creator_tiny_id")]
    public long CreatorTinyId { get; set; }

    [JsonPropertyName("current_slow_mode")]
    public int CurrentSlowMode { get; set; }

    [JsonPropertyName("owner_guild_id")]
    public long OwnerGuildId { get; set; }

    [JsonPropertyName("slow_modes")]
    public List<SlowMode> SlowModes { get; set; }

    [JsonPropertyName("talk_permission")]
    public int TalkPermission { get; set; }

    [JsonPropertyName("visible_type")]
    public int VisibleType { get; set; }
}