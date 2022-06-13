#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.RequestModel
{
    public class SendGroupMsg
    {
        [JsonPropertyName("group_id")]
        public long GroupId { get; set; }
        [JsonPropertyName("message")]
        public string Message { get; set; }
        [JsonPropertyName("auto_escape")]
        public bool AutoEscape { get; set; }

        public SendGroupMsg(string groupId, string message, bool autoEscape = false)
        {
            GroupId = long.Parse(groupId);
            Message = message;
            AutoEscape = autoEscape;
        }
    }
}
