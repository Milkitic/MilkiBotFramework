using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MilkiBotFramework.Messaging.RichMessages
{
    public class RichMessage : IRichMessage
    {
        public IRichMessage[] RichMessages { get; set; }

        public RichMessage(params IRichMessage[] richMessages)
        {
            RichMessages = richMessages;
        }

        public string Encode()
        {
            return string.Join("", RichMessages.Select(k => k.Encode()));
        }
    }
}
