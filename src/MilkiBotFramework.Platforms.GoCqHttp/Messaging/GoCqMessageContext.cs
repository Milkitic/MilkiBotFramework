using System.Text.Json;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging;

public class GoCqMessageContext : MessageContext
{
    public JsonDocument RawJsonDocument { get; internal set; } = null!;
    public MessageBase RawMessage { get; internal set; } = null!;

    public GoCqMessageContext(IRichMessageConverter richMessageConverter) : base(richMessageConverter)
    {
    }
}