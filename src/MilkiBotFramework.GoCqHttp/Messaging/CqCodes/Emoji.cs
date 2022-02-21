using System;
using System.Diagnostics.Contracts;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// emoji表情
    /// </summary>
    public class Emoji : CQCode
    {
        /// <summary>
        /// emoji字符的unicode编号
        /// </summary>
        public string EmojiId { get; }

        /// <summary>
        /// emoji表情。
        /// </summary>
        /// <param name="emojiId">为emoji字符的unicode编号。</param>
        public Emoji(string emojiId)
        {
            Contract.Requires<ArgumentException>(IsNum(emojiId));
            EmojiId = Escape(emojiId);
        }

        public override string ToString() => $"[CQ:emoji,id={EmojiId}]";
    }
}