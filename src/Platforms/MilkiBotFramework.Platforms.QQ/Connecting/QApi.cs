using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApi : IMessageApi
{
    private readonly LightHttpClient _lightHttpClient;
    private readonly QApiConnector _qApiConnector;

    public QApi(LightHttpClient lightHttpClient, IConnector connector)
    {
        _lightHttpClient = lightHttpClient;
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
        var userId = messageContext.MessageUserIdentity!.UserId;

        var host = _qApiConnector.Host;
        var reply = richMessage is RichMessage { FirstIsReply: true };
        object request = reply
            ? new
            {
                content = message,
                msg_type = 0,
                msg_id = messageId,
                msg_seq = _qApiConnector.MessageSequence + Random.Shared.Next(0, 1000)
                //event_id = "C2C_MSG_RECEIVE"
            }
            : new
            {
                content = message,
                msg_type = 0,
            };
        var url = $"https://{host}/v2/groups/{channelId}/messages";
        var result = await _lightHttpClient.HttpPost<object>(url, request, new Dictionary<string, string>
        {
            { "Authorization", _qApiConnector.Authorization }
        });
        var str = result.ToString();
        return "";
        //throw new NotImplementedException();
        //_qApiConnector.Send
    }
}