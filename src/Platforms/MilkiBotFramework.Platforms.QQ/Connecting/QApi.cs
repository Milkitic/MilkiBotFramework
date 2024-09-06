using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApi : IMessageApi
{
    private readonly QApiConnector _qApiConnector;

    public QApi(IConnector connector)
    {
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

    public Task<string> SendPrivateMessageAsync(string userId, string message, IRichMessage? richMessage)
    {
        throw new NotImplementedException();
    }

    public async Task<string> SendChannelMessageAsync(string channelId, string message, IRichMessage? richMessage, string? subChannelId)
    {
        if (richMessage is RichMessage rMessage)
        {
            if (rMessage.FirstIsReply)
            {
                var reply = (Reply)rMessage.First();
                var messageId = reply.MessageId;
            }
        }

        return "";
        //throw new NotImplementedException();
        //_qApiConnector.Send
    }
}