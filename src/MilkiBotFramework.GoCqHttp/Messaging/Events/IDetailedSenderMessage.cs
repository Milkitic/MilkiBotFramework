namespace MilkiBotFramework.GoCqHttp.Messaging.Events
{
    public interface IDetailedSenderMessage
    {
        /// <summary>
        /// 发送人信息。
        /// </summary>
        public SenderBase Sender { get; set; }
    }
}