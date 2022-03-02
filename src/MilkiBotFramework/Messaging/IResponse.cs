using MilkiBotFramework.Messaging.RichMessages;

namespace MilkiBotFramework.Messaging;

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
    IAsyncMessage? AsyncMessage { get; }

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