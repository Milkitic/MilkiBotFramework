using System.Text.Json;
using MilkiBotFramework.GoCqHttp.Messaging.Events;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.GoCqHttp.Messaging;

public class GoCqMessageRequestContext : MessageRequestContext
{
    public GoCqMessageRequestContext(string rawTextMessage) : base(rawTextMessage)
    {
    }

    public JsonDocument RawJsonDocument { get; internal set; }
    public MessageBase RawMessage { get; internal set; }
}