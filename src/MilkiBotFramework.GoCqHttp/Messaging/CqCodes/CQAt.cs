using System;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// @某人
    /// </summary>
    public class CQAt : CQCode
    {
        /// <summary>
        /// 被@的群成员QQ。
        /// </summary>
        public long UserId { get; }

        /// <summary>
        /// @某人。
        /// </summary>
        /// <param name="userId">为被@的群成员QQ。若该参数为all，则@全体成员（次数用尽或权限不足则会转换为文本）。</param>
        public CQAt(long userId)
        {
            UserId = userId;
        }

        internal new static CQAt Parse(string content)
        {
            const int flagLen = 2;
            var dictionary = GetParameters(content.Substring(5 + flagLen, content.Length - 6 - flagLen));
            if (!dictionary.TryGetValue("qq", out var qq))
                throw new InvalidOperationException(nameof(CQAt) + "至少需要qq参数");

            var userId = string.Equals(qq, "all", StringComparison.OrdinalIgnoreCase) ? -1 : long.Parse(qq);
            var cq = new CQAt(userId);
            return cq;
        }

        public override string ToString() => "@" + (UserId == -1 ? "<全体成员>" : UserId);

        public override string Encode() => $"[CQ:at,qq={UserId}]";
    }
}