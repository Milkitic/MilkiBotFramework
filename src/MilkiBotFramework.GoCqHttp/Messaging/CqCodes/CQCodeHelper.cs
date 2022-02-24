using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// CQ码
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static class CQCodeHelper
    {
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
        public static bool IsNum(string str) =>
            str.All(char.IsDigit);

        /// <summary>
        /// 判断文本是否在某一范围（包含界限）
        /// </summary>
        /// <param name="str">要判断的文本。</param>
        /// <param name="lBound">界限的下界。</param>
        /// <param name="uBound">界限的上界。</param>
        /// <returns></returns>
        public static bool InRange(string str, int lBound, int uBound) =>
            int.Parse(str) >= lBound && int.Parse(str) <= uBound;

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
