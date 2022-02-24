using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events
{
    /// <summary>
    /// 加好友请求。
    /// </summary>
    public class FriendRequest
    {
        /// <summary>
        /// 请求类型。
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
        /// 请求 flag，在调用处理请求的 API 时需要传入。
        /// </summary>
        [JsonPropertyName("flag")]
        public long Flag { get; set; }
    }

    /// <summary>
    /// 加好友请求的响应。
    /// </summary>
    public class FriendRequestResp
    {
        /// <summary>
        /// 是否同意请求。
        /// </summary>
        [JsonPropertyName("approve")]
        public bool Approve { get; set; }
        /// <summary>
        /// 添加后的好友备注（仅在同意时有效）。
        /// </summary>
        [JsonPropertyName("remark")]
        public string Remark { get; set; }
    }
}
