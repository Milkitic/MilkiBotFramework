#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.RequestModel
{
    public class SendDiscussMsg
    {
        [JsonPropertyName("discuss_id")]
        public long DiscussId { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
      
        [JsonPropertyName("auto_escape")]
        public bool AutoEscape { get; set; }

        public SendDiscussMsg(string discussId, string message, bool autoEscape = false)
        {
            DiscussId = long.Parse(discussId);
            Message = message;
            AutoEscape = autoEscape;
        }
    }
}
