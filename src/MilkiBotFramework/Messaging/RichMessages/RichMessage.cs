using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MilkiBotFramework.Messaging.RichMessages
{
    public sealed class RichMessage : IRichMessage, IEnumerable<IRichMessage>, IDisposable, IAsyncDisposable
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

        public async ValueTask<string> EncodeAsync()
        {
            var sb = new StringBuilder();
            foreach (var richMessage in RichMessages)
            {
                sb.Append(await richMessage.EncodeAsync());
            }

            return sb.ToString();
        }

        public IEnumerator<IRichMessage> GetEnumerator()
        {
            return RichMessages.SelectMany(CollectionSelector).GetEnumerator();
        }

        private static IEnumerable<IRichMessage> CollectionSelector(IRichMessage richMessage)
        {
            if (richMessage is RichMessage rm)
                foreach (var r in rm.RichMessages.SelectMany(CollectionSelector))
                    yield return r;

            yield return richMessage;
        }

        public override string ToString()
        {
            return string.Join("", RichMessages.Select(k => k.ToString()));
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool FirstIsAt(string userId)
        {
            return RichMessages.Count > 0 &&
                       (RichMessages[0] is At at && at.UserId == userId ||
                        RichMessages[0] is RichMessage rich && rich.FirstIsAt(userId));

        }

        public void Dispose()
        {
            foreach (var richMessage in this)
            {
                if (richMessage is IDisposable d) d.Dispose();
                else if (richMessage is IAsyncDisposable ad) ad.DisposeAsync().AsTask().Wait();
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var richMessage in this)
            {
                if (richMessage is IDisposable d) d.Dispose();
                else if (richMessage is IAsyncDisposable ad) await ad.DisposeAsync();
            }
        }
    }
}
