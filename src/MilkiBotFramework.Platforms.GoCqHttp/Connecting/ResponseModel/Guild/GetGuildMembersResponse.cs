﻿#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel.Guild;

// ReSharper disable once InconsistentNaming
public class GetGuildMembersResponse
{
    [JsonPropertyName("admins")]
    public List<GuildMember> Admins { get; set; }

    [JsonPropertyName("bots")]
    public List<GuildMember> Bots { get; set; }

    [JsonPropertyName("members")]
    public List<GuildMember> Members { get; set; }
}

public class GuildServiceProfile
{
    [JsonPropertyName("nickname")]
    public string Nickname { get; set; }

    [JsonPropertyName("tiny_id")]
    public long TinyId { get; set; }

    [JsonPropertyName("avatar_url")]
    public string AvatarUrl { get; set; }
}