using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Messaging.RequestModel
{
    public class PrivateMsgResponse
    {
        [JsonPropertyName("reply")]
        public string Reply { get; set; }
        [JsonPropertyName("auto_escape")]
        public bool AutoEscape { get; set; }
    }
}
