using System.Collections.Generic;
using System.Linq;

namespace MilkiBotFramework.Messaging.RichMessages
{
    public sealed class RichMessage : IRichMessage
    {
        public List<IRichMessage> RichMessages { get; } = new();

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
    }
}
