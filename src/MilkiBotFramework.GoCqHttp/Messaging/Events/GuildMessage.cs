using System.Text.Json.Serialization;
using MilkiBotFramework.GoCqHttp.Internal;

namespace MilkiBotFramework.GoCqHttp.Messaging.Events
{
    public class GuildMessage : MessageBase, IDetailedSenderMessage
    {
        /// <summary>
        /// 频道号。
        /// </summary>
        [JsonPropertyName("guild_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string GuildId { get; set; }
        /// <summary>
        /// 子频道号。
        /// </summary>
        [JsonPropertyName("channel_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string ChannelId { get; set; }

        [JsonPropertyName("self_tiny_id")]
        [JsonConverter(typeof(Int64ToStringConverter))]
        public string SelfTinyId { get; set; }

        /// <summary>
        /// 发送人信息。
        /// </summary>
        [JsonPropertyName("sender")]
        public SenderBase Sender { get; set; }

        /// <inheritdoc />
        SenderBase IDetailedSenderMessage.Sender { get => Sender; set => Sender = value; }

        //public int ComputedId => HashCode.Combine(GuildId, ChannelId);

    }
}