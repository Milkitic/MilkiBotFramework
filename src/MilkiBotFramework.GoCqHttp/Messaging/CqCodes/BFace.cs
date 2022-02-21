using System;
using System.Diagnostics.Contracts;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 原创表情
    /// </summary>
    public class BFace : CQCode
    {
        /// <summary>
        /// 原创表情的ID。
        /// </summary>
        public string BFaceId { get; }

        /// <summary>
        /// 为该原创表情的ID，存放在酷Q目录的data\bface\下。
        /// </summary>
        /// <param name="bFaceId">原创表情的ID，存放在酷Q目录的data\bface\下。</param>
        public BFace(string bFaceId)
        {
            Contract.Requires<ArgumentException>(IsNum(bFaceId));
            BFaceId = Escape(bFaceId);
        }

        public override string ToString() => $"[CQ:bface,id={BFaceId}]";
    }
}