using System.Text.Json.Serialization;
using MilkiBotFramework.Platforms.GoCqHttp.Internal;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events
{
    /// <summary>
    /// 群消息。
    /// </summary>
    public class GroupMessage : MessageBase, IDetailedSenderMessage
    {
        /// <summary>
        /// 群号。
        /// </summary>
        [JsonPropertyName("group_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string GroupId { get; set; }

        /// <summary>
        /// 匿名信息，如果不是匿名消息则为 null。
        /// </summary>
        [JsonPropertyName("anonymous")]
        public AnonymousObj Anonymous { get; set; }

        /// <summary>
        /// 发送人信息。
        /// </summary>
        [JsonPropertyName("sender")]
        public GroupSender Sender { get; set; }

        /// <inheritdoc />
        SenderBase IDetailedSenderMessage.Sender { get => Sender; set => Sender = (GroupSender)value; }

        public class GroupSender : SenderBase
        {
            /// <summary>
            /// 群名片／备注
            /// </summary>
            [JsonPropertyName("card")]
            public string Card { get; set; }

            /// <summary>
            /// 地区
            /// </summary>
            [JsonPropertyName("area")]
            public string Area { get; set; }

            /// <summary>
            /// 成员等级
            /// </summary>
            [JsonPropertyName("level")]
            public string Level { get; set; }

            /// <summary>
            /// 角色, owner 或 admin 或 member
            /// </summary>
            [JsonPropertyName("role")]
            public string Role { get; set; }

            /// <summary>
            /// 专属头衔
            /// </summary>
            [JsonPropertyName("title")]
            public string Title { get; set; }
        }

        /// <summary>
        /// 匿名信息，如果不是匿名消息则为 null。
        /// </summary>
        public class AnonymousObj
        {
            /// <summary>
            /// 匿名用户 ID。
            /// </summary>
            [JsonPropertyName("id")]
            public int Id { get; set; }

            /// <summary>
            /// 匿名用户名称。
            /// </summary>
            [JsonPropertyName("name")]
            public string Name { get; set; }

            /// <summary>
            /// 匿名用户 flag，在调用禁言 API 时需要传入。
            /// </summary>
            [JsonPropertyName("flag")]
            public string Flag { get; set; }
        }
    }
}
