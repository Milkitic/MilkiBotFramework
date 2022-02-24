using System.Text.Json;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging;

public class GoCqMessageRequestContext : MessageRequestContext
{
    public GoCqMessageRequestContext(string rawTextMessage, IRichMessageConverter richMessageConverter)
        : base(rawTextMessage, richMessageConverter)
    {
    }
    public JsonDocument RawJsonDocument { get; internal set; }
    public MessageBase RawMessage { get; internal set; }

}