#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events
{
    /// <summary>
    /// 好友添加。
    /// </summary>
    public class FriendAdd : EventBase
    {
        /// <summary>
        /// 事件名。
        /// </summary>
        [JsonPropertyName("request_type")]
        public string RequestType { get; set; }

        /// <summary>
        /// 发送请求的 QQ 号。
        /// </summary>
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        /// <summary>
        /// 验证信息。
        /// </summary>
        [JsonPropertyName("comment")]
        public string Comment { get; set; }

        /// <summary>
        /// 请求 flag, 在调用处理请求的 API 时需要传入。
        /// </summary>
        [JsonPropertyName("flag")]
        public string Flag { get; set; }
    }
}