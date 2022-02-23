using System.Text.Json;
using MilkiBotFramework.GoCqHttp.Messaging.Events;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.GoCqHttp.Messaging;

public class GoCqMessageContext : MessageContext
{
    public GoCqMessageRequestContext GoCqRequest { get; private set; }

    public override MessageRequestContext Request
    {
        get => GoCqRequest;
        set => GoCqRequest = (GoCqMessageRequestContext)value;
    }

    public override MessageResponseContext Response { get; set; }
}

public class GoCqMessageRequestContext : MessageRequestContext
{
    public GoCqMessageRequestContext(string rawTextMessage) : base(rawTextMessage)
    {
    }

    public JsonDocument RawJsonDocument { get; internal set; }
    public MessageBase RawMessage { get; internal set; }
}