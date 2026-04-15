using System.Text.Json;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Platforms.OneBot.Messaging.Events;

namespace MilkiBotFramework.Platforms.OneBot.Messaging;

public class OneBotMessageContext : MessageContext
{
    public JsonDocument RawJsonDocument { get; internal set; } = null!;
    public MessageBase RawMessage { get; internal set; } = null!;

    public OneBotMessageContext(IRichMessageConverter richMessageConverter) : base(richMessageConverter)
    {
    }
}