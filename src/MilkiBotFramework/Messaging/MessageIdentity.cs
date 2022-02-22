using MilkiBotFramework.Dispatching;

namespace MilkiBotFramework.Messaging;

public sealed class MessageIdentity
{
    public MessageIdentity(string id, MessageType messageType)
    {
        Id = id;
        MessageType = messageType;
    }

    public MessageIdentity(string id, string subId, MessageType messageType)
    {
        Id = id;
        SubId = subId;
        MessageType = messageType;
    }

    public string? Id { get; set; }
    public string? SubId { get; set; }
    public MessageType MessageType { get; set; }

    private MessageIdentity(MessageType messageType)
    {
        MessageType = messageType;
    }

    public static MessageIdentity MetaMessage { get; } = new(MessageType.Meta);
    public static MessageIdentity NoticeMessage { get; } = new(MessageType.Notice);

    public override string ToString()
    {
        if (Id == null) return $"{{{MessageType}}}";
        if (SubId == null) return $"{{{MessageType} {Id}}}";
        return $"{{{MessageType} {Id}.{SubId}}}";
    }
}