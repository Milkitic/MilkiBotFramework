using System;
using System.Diagnostics.Contracts;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 小表情
    /// </summary>
    public class SFace : CQCode
    {
        /// <summary>
        /// 小表情的ID。
        /// </summary>
        public string SFaceId { get; }

        /// <summary>
        /// 小表情。
        /// </summary>
        /// <param name="sFaceId">为该小表情的ID。</param>
        public SFace(string sFaceId)
        {
            Contract.Requires<ArgumentException>(IsNum(sFaceId));
            SFaceId = Escape(sFaceId);
        }

        public override string ToString() => $"[CQ:sface,id={SFaceId}]";
    }
}