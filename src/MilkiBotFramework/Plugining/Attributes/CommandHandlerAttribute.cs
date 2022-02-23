using System;

namespace MilkiBotFramework.Plugining.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CommandHandlerAttribute : Attribute
{
    public CommandHandlerAttribute(string? command = null)
    {
        Command = command;
    }

    public string? Command { get; }
}