using System.Text.Json;
using MilkiBotFramework.GoCqHttp.Messaging.Events;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.GoCqHttp.Messaging;

public record GoCqMessageContext : MessageContext
{
    public GoCqMessageContext(string rawTextMessage) : base(rawTextMessage)
    {
    }

    public JsonDocument RawJsonDocument { get; internal set; }
    public MessageBase RawMessage { get; internal set; }
}