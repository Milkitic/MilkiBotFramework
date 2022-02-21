namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 纯文本
    /// </summary>
    public class Text : CQCode
    {
        /// <summary>
        /// 文本内容。
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// 纯文本。
        /// </summary>
        /// <param name="content">文本内容。</param>
        public Text(string content) => Content = /*Escape*/(content);
        public override string ToString() => Content;
    }
}