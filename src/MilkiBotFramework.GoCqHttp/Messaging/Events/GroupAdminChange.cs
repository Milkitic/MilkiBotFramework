using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Messaging.Events
{
    /// <summary>
    /// 群管理员变动。
    /// </summary>
    public class GroupAdminChange : EventBase
    {
        /// <summary>
        /// 事件名。
        /// </summary>
        [JsonPropertyName("notice_type")]
        public string NoticeType { get; set; }
        /// <summary>
        /// 事件子类型，分别表示设置和取消管理员。
        /// </summary>
        [JsonPropertyName("sub_type")]
        public string SubType { get; set; }
        /// <summary>
        /// 群号。
        /// </summary>
        [JsonPropertyName("group_id")]
        public long GroupId { get; set; }
        /// <summary>
        /// 管理员 QQ 号。
        /// </summary>
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }
    }
}
