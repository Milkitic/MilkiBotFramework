using System.Text.Json.Serialization;
using MilkiBotFramework.Platforms.GoCqHttp.Internal;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel
{
    public class MsgResponse
    {
        [JsonPropertyName("message_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string MessageId { get; set; }
    }
}
