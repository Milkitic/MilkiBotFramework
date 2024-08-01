#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel
{
    public class LoginInfo
    {
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }
    }
}