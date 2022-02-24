using System.Text.Json.Serialization;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events
{
    public class SenderBase
    {
        /// <summary>
        /// 发送者 QQ 号
        /// </summary>
        [JsonPropertyName("user_id")]
        public long UserId { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        [JsonPropertyName("nickname")]
        public string NickName { get; set; }

        /// <summary>
        /// 性别, male 或 female 或 unknown
        /// </summary>
        [JsonPropertyName("sex")]
        public string Sex { get; set; }

        /// <summary>
        /// 年龄
        /// </summary>
        [JsonPropertyName("age")]
        public int Age { get; set; }
    }
}