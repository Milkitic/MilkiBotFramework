using System;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Plugining.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CommandHandlerAttribute : Attribute
{
    public CommandHandlerAttribute(string? command = null)
    {
        Command = command;
    }

    public string? Command { get; }
    public MessageAuthority Authority { get; set; } = MessageAuthority.Public;
    public MessageType AllowedMessageType { get; set; } = MessageType.Private | MessageType.Channel;
}