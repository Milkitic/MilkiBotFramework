#nullable disable

using System.Text.Json.Serialization;
using MilkiBotFramework.Platforms.OneBot.Internal;

namespace MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel
{
    public class FriendInfo
    {
        [JsonPropertyName("user_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string UserId { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }

        [JsonPropertyName("remark")]
        public string Remark { get; set; }
    }
}