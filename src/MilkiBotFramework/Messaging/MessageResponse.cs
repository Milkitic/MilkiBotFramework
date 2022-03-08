using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

internal class MessageResponse : IResponse
{
    public string? Id { get; }
    public string? SubId { get; }
    public MessageType? MessageType { get; }
    public IRichMessage? Message { get; set; }
    public bool? TryReply { get; set; }
    public bool IsHandled { get; set; }
    public bool? IsForced { get; set; }
    public string? TryAt { get; set; }
    public AsyncMessage? AsyncMessage { get; init; }
    public MessageContext? MessageContext { get; internal set; }

    public MessageResponse(string id, string? subId, IRichMessage autoMessage, MessageType messageType)
    {
        Id = id;
        SubId = subId;
        Message = autoMessage;
        MessageType = messageType;
    }

    public MessageResponse(IRichMessage autoMessage, bool tryReply = true)
    {
        Message = autoMessage;
        TryReply = tryReply;
    }

    public MessageResponse(bool nextBlocked)
    {
        IsHandled = nextBlocked;
    }

    IAsyncMessage? IResponse.AsyncMessage => AsyncMessage;
}