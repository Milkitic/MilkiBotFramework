using System;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// @某人
    /// </summary>
    public class CQAt : At
    {
        /// <summary>
        /// 被@的群成员QQ。
        /// </summary>
        public string UserId { get; }

        /// <summary>
        /// @某人。
        /// </summary>
        /// <param name="userId">为被@的群成员QQ。若该参数为all，则@全体成员（次数用尽或权限不足则会转换为文本）。</param>
        public CQAt(string userId) : base(userId)
        {
            UserId = userId;
        }

        internal static CQAt Parse(ReadOnlyMemory<char> content)
        {
            const int flagLen = 2;
            var s = content.Slice(5 + flagLen, content.Length - 6 - flagLen).ToString();
            var dictionary = CQCodeHelper.GetParameters(s);
            if (!dictionary.TryGetValue("qq", out var qq))
                throw new InvalidOperationException(nameof(CQAt) + "至少需要qq参数");

            var userId = string.Equals(qq, "all", StringComparison.OrdinalIgnoreCase) ? "-1" : qq;
            var cq = new CQAt(userId);
            return cq;
        }

        public override string ToString() => "@" + (UserId == "-1" ? "<全体成员>" : UserId);
        public override string Encode() => $"[CQ:at,qq={UserId}]";
    }
}