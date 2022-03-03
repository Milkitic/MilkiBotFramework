using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MilkiBotFramework.Imaging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging.CqCodes;
using MilkiBotFramework.Platforms.GoCqHttp.Utils;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging;

public class GoCqMessageConverter : IRichMessageConverter
{
    public string Encode(IRichMessage message)
    {
        if (message is RichMessage rich)
        {
            var sb = new StringBuilder();
            foreach (var subMessage in rich.RichMessages)
                sb.Append(Encode(subMessage));
            return sb.ToString();
        }

        if (message is At at)
            return new CQAt(at.UserId).Encode();
        if (message is Reply reply)
            return new CQReply(reply.MessageId).Encode();
        if (message is FileImage fileImage)
            return CQImage.FromBytes(File.ReadAllBytes(fileImage.Path)).Encode();
        if (message is LinkImage linkImage)
            return CQImage.FromUri(linkImage.Uri).Encode();
        if (message is MemoryImage memImage)
        {
            using var ms = new MemoryStream();
            switch (memImage.ImageType)
            {
                case ImageType.Jpeg:
                    memImage.ImageSource.Save(ms, new JpegEncoder());
                    break;
                case ImageType.Bmp:
                    memImage.ImageSource.Save(ms, new BmpEncoder());
                    break;
                case ImageType.Gif:
                    memImage.ImageSource.Save(ms, new GifEncoder());
                    break;
                case ImageType.Png:
                    memImage.ImageSource.Save(ms, new PngEncoder());
                    break;
                case ImageType.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return CQImage.FromBytes(ms.ToArray()).Encode();
        }
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

        switch (type)
        {
            case "image":
                var image = CQImage.Parse(message);
                if (image.DownloadUri != null) return new LinkImage(image.DownloadUri);
                return image;
            case "face":
                return CQFace.Parse(message);
            case "at":
                return new At(CQAt.Parse(message).UserId);
            case "reply":
                return new Reply(CQReply.Parse(message).MessageId);
            default:
                return new CQUnknown(type, message.ToString());
        }
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