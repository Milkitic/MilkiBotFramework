using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging.CqCodes
{
    /// <summary>
    /// QQ表情
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public class CQFace : IRichMessage
    {
        /// <summary>
        /// QQ表情ID。
        /// </summary>
        public int FaceId { get; }

        /// <summary>
        /// QQ表情。
        /// </summary>
        /// <param name="faceId">QQ表情ID，为0-170的数字。</param>
        public CQFace(int faceId)
        {
            //Contract.Requires<ArgumentException>(IsNum(faceId));
            //Contract.Requires<IndexOutOfRangeException>(InRange(faceId, 0, 170));

            FaceId = faceId;
        }

        public ValueTask<string> EncodeAsync() => ValueTask.FromResult($"[CQ:face,id={FaceId}]");

        public override string ToString() => $"[表情{FaceId}]";


        internal static CQFace Parse(ReadOnlyMemory<char> content)
        {
            const int flagLen = 4;
            var s = content.Slice(5 + flagLen, content.Length - 6 - flagLen).ToString();
            var dictionary = CQCodeHelper.GetParameters(s);

            if (!dictionary.TryGetValue("id", out var id))
                throw new InvalidOperationException(nameof(CQFace) + "至少需要id参数");

            var cqFace = new CQFace(Convert.ToInt32(id));

            return cqFace;
        }

        public static string FaceToEmoji(int faceId)
        {
            return faceId switch
            {
                14 => "🙂",
                1 => "😖",
                2 => "😍",
                3 => "😨",
                4 => "😎",
                5 => "😭",
                6 => "😌",
                7 => "🤐",
                8 => "😴",
                9 => "🥺",
                10 => "😓",
                11 => "😡",

                12 => "🤪",
                13 => "😁",
                0 => "😲",
                15 => "🙁",
                16 => "👽",
                96 => "😥",
                18 => "😫",
                19 => "🤮",
                20 => "🤭",
                21 => "😊",
                22 => "🙄",
                23 => "🙁",

                24 => "😋",
                25 => "🥱",
                26 => "😨",
                27 => "😅",
                28 => "😄",
                29 => "🤠",
                30 => "💪",
                31 => "🤬",
                32 => "🤔",
                33 => "🤫",
                34 => "😵",
                35 => "😩",
                _ => $"[表情{faceId}]"
            };
        }
    }
}