using System.Text;
using System.Text.Json;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.QQ.Messaging;
using MilkiBotFramework.Platforms.QQ.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApi : IMessageApi
{
    private readonly LightHttpClient _lightHttpClient;
    private readonly MinIOController _minIoController;
    private readonly QApiConnector _qApiConnector;

    public QApi(LightHttpClient lightHttpClient, MinIOController minIoController, IConnector connector)
    {
        _lightHttpClient = lightHttpClient;
        _minIoController = minIoController;
        if (connector is QApiConnector qApiConnector)
        {
            Connector = connector;
            _qApiConnector = qApiConnector;
        }
        else
        {
            throw new Exception("Except for IGoCqConnector, but actual is " + connector.GetType());
        }
    }

    public IConnector Connector { get; }

    public Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage, MessageContext messageContext)
    {
        throw new NotImplementedException();
    }

    public async Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, MessageContext messageContext, string? subChannelId)
    {
        var (imageMessages, otherMessages) = await RefactorMessages(richMessage);
        var broadcast = richMessage is RichMessage { FirstIsReply: false };
        var messageId = messageContext.MessageId;
        var host = _qApiConnector.Host;
        var messageUrl = $"https://{host}/v2/groups/{channelId}/messages";

        if (imageMessages.Count <= 0 && otherMessages != null)
        {
            object messageRequest = broadcast
                ? new
                {
                    content = otherMessages,
                    msg_type = 0,
                }
                : new
                {
                    content = otherMessages,
                    msg_type = 0,
                    msg_id = messageId,
                    msg_seq = _qApiConnector.MessageSequence + Random.Shared.Next(0, 1000),
                    event_id = "GROUP_MSG_RECEIVE"
                };
            var result = await _lightHttpClient.HttpPost<object>(messageUrl, messageRequest, new Dictionary<string, string>
            {
                { "Authorization", _qApiConnector.Authorization }
            });
            var str = result.ToString();
            return str ?? "";
        }

        var sb = new StringBuilder();
        for (var i = 0; i < imageMessages.Count; i++)
        {
            var imageMessage = imageMessages[i];
            var content = i == imageMessages.Count - 1 ? otherMessages : null;
            var imageUrl = imageMessage switch
            {
                MemoryImage memoryImage => await _minIoController.UploadImage(memoryImage.ImageSource),
                LinkImage linkImage => linkImage.Uri,
                FileImage fileImage => await _minIoController.UploadImage(fileImage.Path),
                _ => throw new ArgumentOutOfRangeException(nameof(imageMessage), imageMessage.GetType(), null)
            };

            object uploadRequest = new
            {
                file_type = 1,
                url = imageUrl,
                srv_send_msg = false
            };

            var uploadUrl = $"https://{host}/v2/groups/{channelId}/files";
            var uploadResult = await _lightHttpClient.HttpPost<object>(uploadUrl, uploadRequest, new Dictionary<string, string>
            {
                { "Authorization", _qApiConnector.Authorization }
            });

            sb.AppendLine(uploadResult.ToString());
            var fileInfo = ((JsonElement)uploadResult).GetProperty("file_info").GetString();

            object sendImgRequest = broadcast
                ? new
                {
                    content = content,
                    msg_type = 7,
                    media = new
                    {
                        file_info = fileInfo
                    },
                }
                : new
                {
                    content = content,
                    msg_type = 7,
                    media = new
                    {
                        file_info = fileInfo
                    },
                    msg_id = messageId,
                    msg_seq = _qApiConnector.MessageSequence + Random.Shared.Next(0, 1000),
                    event_id = "GROUP_MSG_RECEIVE"
                };
            var sendImgResult = await _lightHttpClient.HttpPost<object>(messageUrl, sendImgRequest, new Dictionary<string, string>
            {
                { "Authorization", _qApiConnector.Authorization }
            });

            sb.AppendLine(sendImgResult.ToString());
        }

        return sb.ToString();
    }

    private static async Task<(List<IRichMessage>, string?)> RefactorMessages(IRichMessage? richMessage)
    {
        if (richMessage is null or At) return ([], null);
        if (richMessage is not RichMessage rm) return ([], await richMessage.EncodeAsync());

        Func<IRichMessage, int, KeyValuePair<IRichMessage, int>> selector = static (k, i) =>
            new KeyValuePair<IRichMessage, int>(k, i);

        var messages = rm.FirstIsReply
            ? rm.Select(selector).Skip(1)
            : rm.Select(selector);

        var imageMessages = new List<IRichMessage>();
        var allMessages = new Dictionary<IRichMessage, int>();
        int imageIndex = 1;
        int i = 0;
        foreach (var (message, index) in messages)
        {
            if (message is MemoryImage or FileImage or LinkImage)
            {
                imageMessages.Add(message);
                allMessages.Add(new ImagePlaceholder(imageIndex++), i);
            }
            else
            {
                allMessages.Add(message, i);
            }

            i++;
        }

        var hasImages = imageMessages.Count > 0;
        if (!hasImages) return (imageMessages, await new RichMessage(allMessages.Keys).EncodeAsync());

        var allPlaceholder = allMessages.All(k => k.Key is ImagePlaceholder);
        if (allPlaceholder) return (imageMessages, null);

        var hasInsertText = GetHasInsertedText(allMessages);

        if (hasInsertText) return (imageMessages, await new RichMessage(allMessages.Keys).EncodeAsync());
        var finalText = IsRegularMessage(allMessages.Keys.First())
            ? await new RichMessage(allMessages.Keys.First(), new Text("(见上图)")).EncodeAsync()
            : await new RichMessage(new Text("(见上图)"), allMessages.Keys.Last()).EncodeAsync();

        return (imageMessages, finalText);
    }

    private static bool GetHasInsertedText(Dictionary<IRichMessage, int> allMessages)
    {
        if (allMessages.Count == 0) return false;
        if (IsRegularMessage(allMessages.First().Key))
        {
            return HasInsertedText(allMessages);
        }

        var reverse = allMessages.Reverse().ToArray();
        if (IsRegularMessage(reverse.First().Key))
        {
            return HasInsertedText(reverse);
        }

        return true;
    }

    private static bool HasInsertedText(IReadOnlyCollection<KeyValuePair<IRichMessage, int>> enumerable)
    {
        var firstImageMessage = enumerable
            .Skip(1)
            .First(k => k.Key is ImagePlaceholder);

        return enumerable
            .Skip(firstImageMessage.Value + 1)
            .Any(k => k.Value > firstImageMessage.Value && IsRegularMessage(k.Key));
    }

    private static bool IsRegularMessage(IRichMessage message)
    {
        return message is not ImagePlaceholder;
    }
}