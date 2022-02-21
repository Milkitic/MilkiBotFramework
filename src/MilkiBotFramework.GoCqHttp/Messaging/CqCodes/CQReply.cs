using System;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 回复
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class CQReply : CQCode
    {
        /// <summary>
        /// 被@回复时所引用的消息id, 必须为本群消息。
        /// </summary>
        public string MessageId { get; }

        /// <summary>
        /// 回复。
        /// </summary>
        /// <param name="messageId">被@回复时所引用的消息id, 必须为本群消息。</param>
        public CQReply(string messageId)
        {
            //Contract.Requires<ArgumentException>(IsNum(messageId) || messageId.ToLower() == "all");
            MessageId = messageId;
        }

        internal new static CQReply Parse(string content)
        {
            const int flagLen = 5;
            var dictionary = GetParameters(content.Substring(5 + flagLen, content.Length - 6 - flagLen));
            if (!dictionary.TryGetValue("id", out var id))
                throw new InvalidOperationException(nameof(CQReply) + "至少需要id参数");

            //var messageId = int.Parse(id);
            var cq = new CQReply(id);
            return cq;
        }

        public override string ToString() => $"[回复]";
        public override string Encode() => $"[CQ:reply,id={MessageId}]";
    }
}