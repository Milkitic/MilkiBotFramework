using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MilkiBotFramework.GoCqHttp.Utils;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// CQ码
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public abstract class CQCode
    {
        /// <summary>
        /// 纯文本。
        /// </summary>
        /// <param name="text">文本内容。</param>
        public static implicit operator CQCode(string text) =>
            new Text(text);

        public static CQCode Parse(string content)
        {
            var match = RegexHelper.CqcodeRegex.Matches(content);
            if (match.Count != 1 && match[0].Index != 0 && match[0].Length != content.Length)
            {
                throw new InvalidOperationException("The provided content is not a valid CQCode.");
            }

            var span = content.AsSpan(4, content.Length - 5);

            var i = span.IndexOf(',');
            var type = i > 0 ? span.Slice(0, i).ToString() : span.ToString();

            switch (type)
            {
                case "image":
                    return CQImage.Parse(content);
                case "face":
                    return CQFace.Parse(content);
                case "at":
                    return CQAt.Parse(content);
                case "reply":
                    return CQReply.Parse(content);
                default:
                    return new CQUnknown(type, content);
            }
        }

        public static string Escape(string text)
        {
            var sb = new StringBuilder(text);
            return sb.Replace("&", "&amp;")
                .Replace("[", "&#91;")
                .Replace("]", "&#93;")
                .Replace(",", "&#44;")
                .ToString();
        }

        public static string AntiEscape(string text)
        {
            var sb = new StringBuilder(text);
            return sb.Replace("&#91;", "[")
                .Replace("&#93;", "]")
                .Replace("&#44;", ",")
                .Replace("&amp;", "&")
                .ToString();
        }

        /// <summary>
        /// 判断文本是否为数字。
        /// </summary>
        /// <param name="str">要判断的文本。</param>
        /// <returns></returns>
        protected static bool IsNum(string str) =>
            str.All(char.IsDigit);

        /// <summary>
        /// 判断文本是否在某一范围（包含界限）
        /// </summary>
        /// <param name="str">要判断的文本。</param>
        /// <param name="lBound">界限的下界。</param>
        /// <param name="uBound">界限的上界。</param>
        /// <returns></returns>
        protected static bool InRange(string str, int lBound, int uBound) =>
            int.Parse(str) >= lBound && int.Parse(str) <= uBound;

        public virtual string Encode()
        {
            return ToString();
        }

        public static Dictionary<string, string> GetParameters(string content)
        {
            return content
                .Split(',')
                .Select(k =>
                {
                    var i = k.IndexOf('=');
                    return new KeyValuePair<string, string>(AntiEscape(k[..i]), AntiEscape(k[(i + 1)..]));
                })
                .ToDictionary(k => k.Key, k => k.Value);
        }
    }
}
