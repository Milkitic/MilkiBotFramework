namespace MilkiBotFramework.Messaging;

public sealed class MessageIdentity
{
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is MessageIdentity other && Equals(other);
    }

    private bool Equals(MessageIdentity other)
    {
        return Id == other.Id && SubId == other.SubId && MessageType == other.MessageType;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, SubId, (int)MessageType);
    }

    public static bool operator ==(MessageIdentity? left, MessageIdentity? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MessageIdentity? left, MessageIdentity? right)
    {
        return !Equals(left, right);
    }

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

    public string? Id { get; }
    public string? SubId { get; }
    public MessageType MessageType { get; }

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