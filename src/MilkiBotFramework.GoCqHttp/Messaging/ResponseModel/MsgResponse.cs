using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Messaging.ResponseModel
{
    public class MsgResponse
    {
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }
    }
}
