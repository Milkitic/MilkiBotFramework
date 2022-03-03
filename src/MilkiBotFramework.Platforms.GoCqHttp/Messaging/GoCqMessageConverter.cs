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
    public async ValueTask<string> EncodeAsync(IRichMessage message)
    {
        if (message is RichMessage rich)
        {
            var sb = new StringBuilder();
            foreach (var subMessage in rich.RichMessages)
                sb.Append(await EncodeAsync(subMessage));
            return sb.ToString();
        }

        if (message is At at)
            return await new CQAt(at.UserId).EncodeAsync();
        if (message is Reply reply)
            return await new CQReply(reply.MessageId).EncodeAsync();
        if (message is FileImage fileImage)
            return await CQImage.FromBytes(File.ReadAllBytes(fileImage.Path)).EncodeAsync();
        if (message is LinkImage linkImage)
            return await CQImage.FromUri(linkImage.Uri).EncodeAsync();
        if (message is MemoryImage memImage)
        {
            await using var ms = new MemoryStream();
            switch (memImage.ImageType)
            {
                case ImageType.Jpeg:
                    await memImage.ImageSource.SaveAsync(ms, new JpegEncoder());
                    break;
                case ImageType.Bmp:
                    await memImage.ImageSource.SaveAsync(ms, new BmpEncoder());
                    break;
                case ImageType.Gif:
                    await memImage.ImageSource.SaveAsync(ms, new GifEncoder());
                    break;
                case ImageType.Png:
                    await memImage.ImageSource.SaveAsync(ms, new PngEncoder());
                    break;
                case ImageType.Unknown:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return await CQImage.FromBytes(ms.ToArray()).EncodeAsync();
        }
        return await message.EncodeAsync();
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