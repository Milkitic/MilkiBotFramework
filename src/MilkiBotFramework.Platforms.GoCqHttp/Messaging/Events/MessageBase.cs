using System.Text.Json.Serialization;
using MilkiBotFramework.Platforms.GoCqHttp.Internal;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events
{
    /// <summary>
    /// 私聊消息。
    /// </summary>
    public abstract class MessageBase : EventBase
    {
        /// <summary>
        /// 消息类型。
        /// </summary>
        [JsonPropertyName("message_type")]
        public string MessageType { get; set; }

        /// <summary>
        /// 消息子类型，如果是好友则是 friend，是群临时会话则是 group。
        /// </summary>
        [JsonPropertyName("sub_type")]
        public string SubType { get; set; }

        /// <summary>
        /// 消息 ID。
        /// </summary>
        [JsonPropertyName("message_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string MessageId { get; set; }

        /// <summary>
        /// 发送者 QQ 号。
        /// </summary>
        [JsonPropertyName("user_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string UserId { get; set; }

        /// <summary>
        /// 消息内容。
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }

        /// <summary>
        /// 原始消息内容。
        /// </summary>
        [JsonPropertyName("raw_message")]
        public string RawMessage { get; set; }

        /// <summary>
        /// 字体。
        /// </summary>
        [JsonPropertyName("font")]
        public long Font { get; set; }
    }
}