using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Messaging.ResponseModel
{
    public class StrangerInfo
    {
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }
        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }
        [JsonPropertyName("sex")]
        public string Sex { get; set; }
        [JsonPropertyName("age")]
        public string Age { get; set; }
    }
}
