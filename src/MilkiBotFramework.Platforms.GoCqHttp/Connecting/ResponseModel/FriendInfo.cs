using System.Text.Json.Serialization;
using MilkiBotFramework.Platforms.GoCqHttp.Internal;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel
{
    public class FriendInfo
    {
        [JsonPropertyName("user_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string UserId { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }

        [JsonPropertyName("remark")]
        public string? Remark { get; set; }
    }
}