using System.Text.Json.Serialization;
using MilkiBotFramework.GoCqHttp.Internal;

namespace MilkiBotFramework.GoCqHttp.Connecting.ResponseModel
{
    public class MsgResponse
    {
        [JsonPropertyName("message_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string MessageId { get; set; }
    }
}
