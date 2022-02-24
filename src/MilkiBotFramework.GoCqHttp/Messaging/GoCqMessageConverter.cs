using System;
using System.Collections.Generic;
using System.Linq;
using MilkiBotFramework.GoCqHttp.Messaging.CqCodes;
using MilkiBotFramework.GoCqHttp.Utils;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.GoCqHttp.Messaging;

public class GoCqMessageConverter : IRichMessageConverter
{
    public string Encode(IRichMessage message)
    {
        return message.Encode();
    }

    public RichMessage Decode(ReadOnlyMemory<char> message)
    {
        var matches = RegexHelper.CqcodeRegex.Matches(message.ToString());
        if (matches.Count == 0)
        {
            Text text = CQCodeHelper.AntiEscape(message.ToString());
            return new RichMessage(text);
        }

        var flatSpans = new List<IRichMessage>();

        var tempRanges = new List<(int index, int count, bool isRaw)>();
        tempRanges.AddRange(matches.Select(k => (k.Index, k.Length, false)));
        FillRawRanges(message, tempRanges);
        tempRanges.Sort(RangeComparer.Instance);


        foreach ((int index, int count, bool isRaw) in tempRanges)
        {
            var subMessage = message.Slice(index, count);

            if (!isRaw)
            {
                flatSpans.Add(DecodeSingle(subMessage));
            }
            else
            {
                Text text = CQCodeHelper.AntiEscape(subMessage.ToString());
                flatSpans.Add(text);
            }
        }

        return new RichMessage(flatSpans);
    }

    private IRichMessage DecodeSingle(ReadOnlyMemory<char> message)
    {
        var subSpan = message.Slice(4, message.Length - 5).Span;

        var i = subSpan.IndexOf(',');
        var type = i > 0 ? subSpan.Slice(0, i).ToString() : subSpan.ToString();

        return type switch
        {
            "image" => CQImage.Parse(message),
            "face" => CQFace.Parse(message),
            "at" => CQAt.Parse(message),
            "reply" => CQReply.Parse(message),
            _ => new CQUnknown(type, message.ToString())
        };
    }

    private static void FillRawRanges(ReadOnlyMemory<char> message, List<(int index, int count, bool isRaw)> ranges)
    {
        int preIndex = 0;
        for (var i = 0; i < ranges.Count; i++)
        {
            var (index, count, type) = ranges[i];
            if (index != preIndex)
            {
                var len = index - preIndex;
                ranges.Insert(i, (preIndex, len, true));
                i++;
                preIndex += len;
            }

            preIndex += count;
        }

        if (preIndex != message.Length)
            ranges.Add((preIndex, message.Length - preIndex, true));
    }
}