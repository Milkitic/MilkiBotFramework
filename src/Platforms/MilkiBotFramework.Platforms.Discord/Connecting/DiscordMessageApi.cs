using System.Text;
using Discord;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Imaging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.Discord.Messaging;
using SixLabors.ImageSharp;

namespace MilkiBotFramework.Platforms.Discord.Connecting;

public class DiscordMessageApi : IMessageApi
{
    private readonly DiscordConnector _connector;

    public DiscordMessageApi(IConnector connector)
    {
        if (connector is DiscordConnector discordConnector)
        {
            _connector = discordConnector;
        }
        else
        {
            throw new ArgumentException("Connector must be DiscordConnector");
        }
    }

    public bool Supports(MessageContext messageContext)
    {
        return messageContext is DiscordMessageContext;
    }

    public async Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage,
        MessageContext messageContext, string? subChannelId)
    {
        var targetId = subChannelId ?? channelId;
        if (!ulong.TryParse(targetId, out var id))
            return string.Empty;

        var channel = await _connector.Client.GetChannelAsync(id) as IMessageChannel;
        if (channel == null)
            return string.Empty;

        var reference = GetMessageReference(richMessage);
        var (text, attachments) = await PrepareMessageAsync(message, richMessage);

        try
        {
            if (attachments.Count > 0)
            {
                var sent = await channel.SendFilesAsync(attachments, text, messageReference: reference);
                return sent.Id.ToString();
            }

            var msg = await channel.SendMessageAsync(text, messageReference: reference);
            return msg.Id.ToString();
        }
        finally
        {
            // 释放所有附件流
            foreach (var attachment in attachments)
            {
                await attachment.Stream.DisposeAsync();
            }
        }
    }

    public async Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage,
        MessageContext messageContext)
    {
        if (!ulong.TryParse(userId, out var id))
            return string.Empty;

        var user = await _connector.Client.GetUserAsync(id);
        if (user == null)
            return string.Empty;

        var reference = GetMessageReference(richMessage);
        var (text, attachments) = await PrepareMessageAsync(message, richMessage);

        try
        {
            var dmChannel = await user.CreateDMChannelAsync();
            if (dmChannel == null)
                return string.Empty;

            if (attachments.Count > 0)
            {
                var sent = await dmChannel.SendFilesAsync(attachments, text, messageReference: reference);
                return sent.Id.ToString();
            }

            var msg = await dmChannel.SendMessageAsync(text, messageReference: reference);
            return msg.Id.ToString();
        }
        finally
        {
            // 释放所有附件流
            foreach (var attachment in attachments)
            {
                await attachment.Stream.DisposeAsync();
            }
        }
    }

    private static MessageReference? GetMessageReference(IRichMessage? richMessage)
    {
        if (richMessage is RichMessage rm && rm.FirstIsReply)
        {
            foreach (var sub in rm.RichMessages)
            {
                if (sub is Reply reply && ulong.TryParse(reply.MessageId, out var msgId))
                {
                    return new MessageReference(msgId);
                }
            }
        }

        return null;
    }

    private static async Task<(string Text, List<FileAttachment> Attachments)> PrepareMessageAsync(string message,
        IRichMessage? richMessage)
    {
        var attachments = new List<FileAttachment>();
        var text = message;

        if (richMessage == null)
            return (text, attachments);

        if (richMessage is RichMessage rm)
        {
            var sb = new StringBuilder();
            foreach (var sub in rm.RichMessages)
            {
                if (sub is Reply)
                    continue;

                var (subText, subAttachments) = await EncodeSingleAsync(sub);
                sb.Append(subText);
                attachments.AddRange(subAttachments);
            }

            if (sb.Length > 0)
                text = sb.ToString();
        }
        else
        {
            var (subText, subAttachments) = await EncodeSingleAsync(richMessage);
            if (!string.IsNullOrEmpty(subText))
                text = subText;
            attachments.AddRange(subAttachments);
        }

        return (text, attachments);
    }

    private static async Task<(string Text, List<FileAttachment> Attachments)> EncodeSingleAsync(IRichMessage message)
    {
        var attachments = new List<FileAttachment>();

        if (message is At at)
        {
            return ($"<@{at.UserId}>", attachments);
        }

        if (message is Text text)
        {
            return (text.Content, attachments);
        }

        if (message is LinkImage linkImg)
        {
            return (linkImg.Uri, attachments);
        }

        if (message is FileImage fileImg)
        {
            var stream = File.OpenRead(fileImg.Path);
            var fileName = Path.GetFileName(fileImg.Path);
            attachments.Add(new FileAttachment(stream, fileName));
            return (string.Empty, attachments);
        }

        if (message is MemoryImage memImg)
        {
            var ms = new MemoryStream();
            var (ext, mime) = GetImageFormat(memImg.ImageEncodingOptions.ImageType);
            await SaveMemoryImageAsync(memImg, ms);
            ms.Position = 0;
            attachments.Add(new FileAttachment(ms, $"image.{ext}"));
            return (string.Empty, attachments);
        }

        return (await message.EncodeAsync(), attachments);
    }

    private static async Task SaveMemoryImageAsync(MemoryImage memoryImage, MemoryStream ms)
    {
        switch (memoryImage.ImageEncodingOptions.ImageType)
        {
            case ImageType.Jpeg:
                await memoryImage.ImageSource.SaveAsJpegAsync(ms);
                break;
            case ImageType.Bmp:
                await memoryImage.ImageSource.SaveAsBmpAsync(ms);
                break;
            case ImageType.Gif:
                await memoryImage.ImageSource.SaveAsGifAsync(ms);
                break;
            case ImageType.Png:
                await memoryImage.ImageSource.SaveAsPngAsync(ms);
                break;
            case ImageType.Webp:
                await memoryImage.ImageSource.SaveAsWebpAsync(ms);
                break;
            case ImageType.Unknown:
            default:
                await memoryImage.ImageSource.SaveAsPngAsync(ms);
                break;
        }
    }

    private static (string Extension, string MimeType) GetImageFormat(ImageType imageType)
    {
        return imageType switch
        {
            ImageType.Jpeg => ("jpg", "image/jpeg"),
            ImageType.Bmp => ("bmp", "image/bmp"),
            ImageType.Gif => ("gif", "image/gif"),
            ImageType.Png => ("png", "image/png"),
            ImageType.Webp => ("webp", "image/webp"),
            _ => ("png", "image/png")
        };
    }
}