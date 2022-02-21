namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 发送链接分享
    /// </summary>
    public class Share : CQCode
    {
        /// <summary>
        /// 分享链接。
        /// </summary>
        public string LinkUrl { get; }

        /// <summary>
        /// 分享的标题。
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 分享的简介。
        /// </summary>
        public string Content { get; }

        /// <summary>
        /// 分享的图片链接。
        /// </summary>
        public string ImageUrl { get; }

        /// <summary>
        /// 发送链接分享。注意：链接分享只能作为单独的一条消息发送。
        /// </summary>
        /// <param name="linkUrl">为分享链接。</param>
        /// <param name="title">为分享的标题，建议12字以内。</param>
        /// <param name="content">为分享的简介，建议30字以内。该参数可被忽略。</param>
        /// <param name="imageUrl">为分享的图片链接。若参数为空或被忽略，则显示默认图片。</param>
        public Share(string linkUrl, string title, string content, string imageUrl)
        {
            LinkUrl = Escape(linkUrl);
            Title = Escape(title);
            Content = Escape(content);
            ImageUrl = Escape(imageUrl);
        }

        public override string ToString() =>
            string.Format("[CQ:share,url={0},title={1},content={2},image={3}]",
                LinkUrl, Title, Content, ImageUrl);
    }
}