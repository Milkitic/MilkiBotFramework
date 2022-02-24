using System;
using System.Text.Json.Serialization;
using MilkiBotFramework.Platforms.GoCqHttp.Internal;

namespace MilkiBotFramework.Platforms.GoCqHttp.Connecting.ResponseModel
{
    public class GetMsgResponse
    {
        [JsonPropertyName("group")]
        public bool Group { get; set; }

        [JsonPropertyName("group_id")]
        public long GroupId { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("message_id")]
        public string MessageId { get; set; }

        [JsonPropertyName("message_seq")]
        public long MessageSeq { get; set; }

        [JsonPropertyName("message_type")]
        public string MessageType { get; set; }

        [JsonPropertyName("raw_message")]
        public string RawMessage { get; set; }

        [JsonPropertyName("real_id")]
        public long RealId { get; set; }

        [JsonPropertyName("sender")]
        public Sender Sender { get; set; }

        [JsonPropertyName("time")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTimeOffset Time { get; set; }
    }
    public class Sender
    {
        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }

        [JsonPropertyName("user_id")]
        public long UserId { get; set; }
    }
}