using System.Text.Json;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Platforms.QQ.Messaging;

public class QMessageContext : MessageContext
{
    public JsonDocument RawJsonDocument { get; internal set; } = null!;
    public string RawMessage { get; internal set; } = null!;

    public QMessageContext(IRichMessageConverter richMessageConverter) : base(richMessageConverter)
    {
    }
}