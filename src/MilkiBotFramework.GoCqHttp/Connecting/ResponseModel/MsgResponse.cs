using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Connecting.ResponseModel
{
    public class MsgResponse
    {
        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }
    }
}
