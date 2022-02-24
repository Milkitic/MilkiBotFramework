using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// 发送音乐自定义分享
    /// </summary>
    public class CQMusic : IRichMessage
    {
        /// <summary>
        /// 分享链接。
        /// </summary>
        public string LinkUrl { get; }

        /// <summary>
        /// 音频链接。
        /// </summary>
        public string AudioUrl { get; }

        /// <summary>
        /// 音乐的标题。
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 音乐的简介。
        /// </summary>
        public string? Content { get; }

        /// <summary>
        /// 音乐的封面图片链接。
        /// </summary>
        public string? ImageUrl { get; }

        /// <summary>
        /// 发送音乐自定义分享。注意：音乐自定义分享只能作为单独的一条消息发送。
        /// </summary>
        /// <param name="linkUrl">为分享链接，即点击分享后进入的音乐页面（如歌曲介绍页）。</param>
        /// <param name="audioUrl">为音频链接（如mp3链接）。</param>
        /// <param name="title">为音乐的标题，建议12字以内。</param>
        /// <param name="content">为音乐的简介，建议30字以内。该参数可被忽略。</param>
        /// <param name="imageUrl">为音乐的封面图片链接。若参数为空或被忽略，则显示默认图片。</param>
        public CQMusic(string linkUrl, string audioUrl, string title, string? content = null, string? imageUrl = null)
        {
            LinkUrl = CQCodeHelper.Escape(linkUrl);
            AudioUrl = CQCodeHelper.Escape(audioUrl);
            Title = CQCodeHelper.Escape(title);
            Content = content == null ? null : CQCodeHelper.Escape(content);
            ImageUrl = imageUrl == null ? null : CQCodeHelper.Escape(imageUrl);
        }

        public override string ToString() => "[音乐自定义分享]";

        public string Encode() =>
            string.Format("[CQ:music,type=custom,url={0},audio={1},title={2},content={3},image={4}]",
                LinkUrl, AudioUrl, Title, Content, ImageUrl);
    }
}