using System;

namespace MilkiBotFramework.Messaging;

public sealed class MessageUserIdentity
{
    public MessageUserIdentity(MessageIdentity messageIdentity, string userId)
    {
        MessageIdentity = messageIdentity;
        UserId = userId;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((MessageUserIdentity)obj);
    }

    private bool Equals(MessageUserIdentity other)
    {
        return MessageIdentity.Equals(other.MessageIdentity) && UserId == other.UserId;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(MessageIdentity, UserId);
    }

    public static bool operator ==(MessageUserIdentity? left, MessageUserIdentity? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(MessageUserIdentity? left, MessageUserIdentity? right)
    {
        return !Equals(left, right);
    }

    public MessageIdentity MessageIdentity { get; }
    public string UserId { get; }
}