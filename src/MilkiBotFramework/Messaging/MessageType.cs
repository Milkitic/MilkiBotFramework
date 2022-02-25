using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

public enum MessageType
{
    Private, Channel, Notice, Meta
}

internal class MessageResponse : IResponse
{
    public string? Id { get; }
    public string? SubId { get; }
    public MessageType? MessageType { get; }
    public IRichMessage? Message { get; set; }
    public bool TryReply { get; set; }
    public bool IsHandled { get; set; }
    public bool IsForced { get; set; }
    public string? TryAt { get; set; }

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
}

public interface IResponse
{
    string? Id { get; }
    string? SubId { get; }
    MessageType? MessageType { get; }
    IRichMessage? Message { get; set; }
    bool TryReply { get; set; }
    bool IsHandled { get; set; }
    bool IsForced { get; set; }
    string? TryAt { get; set; }

    public IResponse Handled()
    {
        IsHandled = true;
        return this;
    }
    public IResponse Forced()
    {
        IsForced = true;
        return this;
    }
    public IResponse At(string? id)
    {
        TryAt = id;
        return this;
    }
}