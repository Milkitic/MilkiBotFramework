#nullable disable

using System.Text.Json.Serialization;
using MilkiBotFramework.Platforms.OneBot.Internal;

namespace MilkiBotFramework.Platforms.OneBot.Connecting.ResponseModel
{
    public class MsgResponse
    {
        [JsonPropertyName("message_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string MessageId { get; set; }
    }
}
