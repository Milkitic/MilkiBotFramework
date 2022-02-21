using System.Text.Json.Serialization;

namespace MilkiBotFramework.GoCqHttp.Messaging.Events
{
    // seealso: https://ishkong.github.io/go-cqhttp-docs/event/#私聊消息
    public class PrivateMessage : MessageBase, IDetailedSenderMessage
    {
        /// <summary>
        /// 发送人信息
        /// </summary>
        [JsonPropertyName("sender")]
        public PrivateSender Sender { get; set; }

        /// <inheritdoc />
        SenderBase IDetailedSenderMessage.Sender { get => Sender; set => Sender = (PrivateSender)value; }

        public class PrivateSender : SenderBase
        {
        }
    }
}