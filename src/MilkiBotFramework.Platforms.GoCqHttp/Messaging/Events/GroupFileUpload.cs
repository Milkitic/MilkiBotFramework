#nullable disable

using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events
{
    /// <summary>
    /// 群文件上传。
    /// </summary>
    public class GroupFileUpload : EventBase
    {
        /// <summary>
        /// 事件名。
        /// </summary>
        [JsonPropertyName("notice_type")]
        public string NoticeType { get; set; }

        /// <summary>
        /// 群号。
        /// </summary>
        [JsonPropertyName("group_id")]
        public long GroupId { get; set; }

        /// <summary>
        /// 发送者 QQ 号。
        /// </summary>
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        /// <summary>
        /// 文件信息。
        /// </summary>
        [JsonPropertyName("file")]
        public FileObj File { get; set; }

        /// <summary>
        /// 文件信息。
        /// </summary>
        public class FileObj
        {
            /// <summary>
            /// 文件 ID。
            /// </summary>
            [JsonPropertyName("id")]
            public string Id { get; set; }

            /// <summary>
            /// 文件名。
            /// </summary>
            [JsonPropertyName("name")]
            public string Name { get; set; }

            /// <summary>
            /// 文件大小（字节数）。
            /// </summary>
            [JsonPropertyName("size")]
            public long Size { get; set; }

            /// <summary>
            /// busid（目前不清楚有什么作用）。
            /// </summary>
            [JsonPropertyName("busid")]
            public int BusId { get; set; }
        }
    }
}
