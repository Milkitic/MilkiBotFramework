using System.Collections.Generic;
using System.Linq;

namespace MilkiBotFramework.Messaging.RichMessages
{
    public sealed class RichMessage : IRichMessage
    {
        public List<IRichMessage> RichMessages { get; } = new();

        public bool FirstIsReply => RichMessages.Count > 0 &&
                                    (RichMessages[0] is Reply || RichMessages[0] is RichMessage { FirstIsReply: true });

        public RichMessage(IEnumerable<IRichMessage> richMessages)
        {
            RichMessages.AddRange(richMessages);
        }

        public RichMessage(params IRichMessage[] richMessages)
        {
            RichMessages.AddRange(richMessages);
        }

        public string Encode()
        {
            return string.Join("", RichMessages.Select(k => k.Encode()));
        }

        public override string ToString()
        {
            return string.Join("", RichMessages.Select(k => k.ToString()));
        }

        public bool FirstIsAt(string userId)
        {
            return RichMessages.Count > 0 &&
                       (RichMessages[0] is At at && at.UserId == userId ||
                        RichMessages[0] is RichMessage rich && rich.FirstIsAt(userId));

        }
    }
}
