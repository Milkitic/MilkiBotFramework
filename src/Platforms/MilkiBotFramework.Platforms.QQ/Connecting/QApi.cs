using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApi : IMessageApi
{
    public Task<string> SendPrivateMessageAsync(string userId, string message)
    {
        throw new NotImplementedException();
    }

    public Task<string> SendChannelMessageAsync(string channelId, string message, string? subChannelId)
    {
        throw new NotImplementedException();
    }
}