using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MilkiBotFramework.GoCqHttp.Internal;

namespace MilkiBotFramework.GoCqHttp.Messaging.ResponseModel
{
    public class GuildInfo
    {
        [JsonPropertyName("guild_id")]
        public long GuildId { get; set; }

        [JsonPropertyName("group_name")]
        public string GuildName { get; set; }

        [JsonPropertyName("group_memo")]
        public string GroupMemo { get; set; }

        [JsonPropertyName("group_create_time")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTimeOffset GroupCreateTime { get; set; }

        [JsonPropertyName("group_level")]
        public int GroupLevel { get; set; }

        [JsonPropertyName("member_count")]
        public int MemberCount { get; set; }

        [JsonPropertyName("max_member_count")]
        public int MaxMemberCount { get; set; }

        [JsonIgnore]
        public List<GroupMember> Members { get; set; }
    }
    public class GroupInfo
    {
        [JsonPropertyName("group_id")]
        public long GroupId { get; set; }

        [JsonPropertyName("group_name")]
        public string GroupName { get; set; }

        [JsonPropertyName("group_memo")]
        public string GroupMemo { get; set; }

        [JsonPropertyName("group_create_time")]
        //[JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTimeOffset GroupCreateTime { get; set; }

        [JsonPropertyName("group_level")]
        public int GroupLevel { get; set; }

        [JsonPropertyName("member_count")]
        public int MemberCount { get; set; }

        [JsonPropertyName("max_member_count")]
        public int MaxMemberCount { get; set; }

        [JsonIgnore]
        public List<GroupMember> Members { get; set; }
        //[JsonIgnore]
        //public long SubId { get; set; }
    }
}
