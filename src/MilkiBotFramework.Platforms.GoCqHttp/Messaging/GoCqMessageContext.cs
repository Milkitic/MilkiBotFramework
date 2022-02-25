using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging.Events;

namespace MilkiBotFramework.Platforms.GoCqHttp.Messaging;

public class GoCqMessageContext : MessageContext
{
    public JsonDocument RawJsonDocument { get; internal set; }
    public MessageBase RawMessage { get; internal set; }

    public GoCqMessageContext(IRichMessageConverter richMessageConverter,
        IMessageApi messageApi,
        ILogger<GoCqMessageContext> logger)
        : base(richMessageConverter, messageApi, logger)
    {
    }
}