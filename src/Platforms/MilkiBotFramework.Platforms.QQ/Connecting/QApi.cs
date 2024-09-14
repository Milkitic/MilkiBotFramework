using System.Text.Json;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.QQ.Messaging;

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
        var messageId = messageContext.MessageId;
        //var userId = messageContext.MessageUserIdentity!.UserId;

        var host = _qApiConnector.Host;
        Dictionary<MemoryImage, string?> memoryImages;
        bool callTextReq;
        if (richMessage is RichMessage rm)
        {
            var left = rm.ToHashSet();
            memoryImages = rm.OfType<MemoryImage>().ToDictionary(k => k, k => default(string));
            left.ExceptWith(memoryImages.Keys);
            callTextReq = left.Count == 0 || left.Count == 1 && left.First() is At;
            richMessage = new RichMessage(left);
        }
        else
        {
            callTextReq = true;
            memoryImages = new Dictionary<MemoryImage, string?>();
        }

        foreach (var memoryImage in memoryImages.Keys)
        {
            var imageUrl = await _minIoController.UploadImage(memoryImage.ImageSource);
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

            var str = uploadResult.ToString();
            //var fileInfo = ((JsonElement)uploadResult).GetProperty("file_info").GetString();
            var fileInfo = ((JsonElement)uploadResult).GetProperty("file_info").GetString();
            memoryImages[memoryImage] = fileInfo;
        }

        var reply = richMessage is RichMessage { FirstIsReply: true };
        var sendUrl = $"https://{host}/v2/groups/{channelId}/messages";
        if (callTextReq)
        {
            object request = reply
                ? new
                {
                    content = message,
                    msg_type = 0,
                    msg_id = messageId,
                    msg_seq = _qApiConnector.MessageSequence + Random.Shared.Next(0, 1000),
                    event_id = "GROUP_MSG_RECEIVE"
                }
                : new
                {
                    content = message,
                    msg_type = 0,
                };
            var result = await _lightHttpClient.HttpPost<object>(sendUrl, request, new Dictionary<string, string>
            {
                { "Authorization", _qApiConnector.Authorization }
            });
            var str = result.ToString();

        }

        foreach (var kvp in memoryImages)
        {
            object sendImgRequest = reply
                ? new
                {
                    content = "test",
                    msg_type = 7,
                    media = new
                    {
                        file_info = kvp.Value
                    },
                    msg_id = messageId,
                    msg_seq = _qApiConnector.MessageSequence + Random.Shared.Next(0, 1000),
                    event_id = "GROUP_MSG_RECEIVE"
                }
                : new
                {
                    content = "test",
                    msg_type = 7,
                    media = kvp.Value,
                };
            var sendImgResult = await _lightHttpClient.HttpPost<object>(sendUrl, sendImgRequest, new Dictionary<string, string>
            {
                { "Authorization", _qApiConnector.Authorization }
            });
        }

        return  "";
    }
}
