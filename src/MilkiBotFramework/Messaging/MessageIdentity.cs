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

    public MessageIdentity(string id, string? subId, MessageType messageType)
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

    public static MessageIdentity Parse(string str)
    {
        var span = str.AsSpan().Trim();
        if (span.Length < 2) throw new ArgumentException("Not a valid message identity.", nameof(str));
        span = span[1..^1];

        var spaceIndex = span.IndexOf(' ');
        if (spaceIndex < 0) throw new ArgumentException("Not a valid message identity.", nameof(str));
        var typeStr = span[..spaceIndex];

        var messageType = Enum.Parse<MessageType>(typeStr);

        var ids = span[(spaceIndex + 1)..];
        var dotIndex = ids.IndexOf('.');

        string id;
        string? subId = null;
        if (dotIndex < 0)
        {
            id = ids.ToString();
        }
        else
        {
            id = ids[..dotIndex].ToString();
            var readOnlySpan = ids[(dotIndex + 1)..];
            subId = readOnlySpan.Length == 0 ? null : readOnlySpan.ToString();
        }

        return new MessageIdentity(id, subId, messageType);
    }
}