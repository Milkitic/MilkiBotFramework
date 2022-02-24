using System;
using System.Text.Json.Serialization;
using MilkiBotFramework.Platforms.GoCqHttp.Internal;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events
{
    /// <summary>
    /// 上报消息中含有的字段。
    /// </summary>
    public abstract class EventBase
    {
        /// <summary>
        /// 上报类型，分别为message、notice、request。
        /// </summary>
        [JsonPropertyName("post_type")]
        public string PostType { get; set; }

        /// <summary>
        /// 事件发生的时间戳。
        /// </summary>
        [JsonPropertyName("time")]
        [JsonConverter(typeof(UnixDateTimeConverter))]
        public DateTimeOffset Time { get; set; }

        /// <summary>
        /// 收到消息的机器人 QQ 号。
        /// </summary>
        [JsonPropertyName("self_id")]
        public long SelfId { get; set; }

        //[JsonPropertyName( "target_id")]
        //public string TargetId { get; set; }
    }
}