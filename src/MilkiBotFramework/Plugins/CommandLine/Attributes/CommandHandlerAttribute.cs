using System;

namespace MilkiBotFramework.Plugins.CommandLine.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CommandHandlerAttribute : Attribute
{
    public CommandHandlerAttribute(string? command = null)
    {
        Command = command;
    }

    public string? Command { get; }
}