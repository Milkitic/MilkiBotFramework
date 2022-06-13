#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.RequestModel
{
    public class SendPrivateMsg
    {
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
        [JsonPropertyName("auto_escape")]
        public bool AutoEscape { get; set; }

        public SendPrivateMsg(string userId, string message, bool autoEscape = false)
        {
            UserId = long.Parse(userId);
            Message = message;
            AutoEscape = autoEscape;
        }
    }
}
