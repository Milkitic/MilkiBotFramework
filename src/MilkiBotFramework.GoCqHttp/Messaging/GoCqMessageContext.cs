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