using System;
using System.Text.Json.Serialization;
using MilkiBotFramework.GoCqHttp.Internal;

namespace MilkiBotFramework.GoCqHttp.Connecting.ResponseModel
{
    public class GroupMember
    {
        [JsonPropertyName("group_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string GroupId { get; set; }

        [JsonPropertyName("user_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string UserId { get; set; }

        [JsonPropertyName("nickname")]
        public string? Nickname { get; set; }

        [JsonPropertyName("card")]
        public string? Card { get; set; }

        [JsonPropertyName("sex")]
        public string Sex { get; set; }

        [JsonPropertyName("age")]
        public int Age { get; set; }

        [JsonPropertyName("area")]
        public string Area { get; set; }

        [JsonPropertyName("join_time")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTimeOffset JoinTime { get; set; }

        [JsonPropertyName("last_sent_time")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTimeOffset LastSentTime { get; set; }

        [JsonPropertyName("level")]
        public string Level { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("unfriendly")]
        public bool Unfriendly { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("title_expire_time")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTimeOffset TitleExpireTime { get; set; }

        [JsonPropertyName("card_changeable")]
        public bool CardChangeable { get; set; }

    }
}
