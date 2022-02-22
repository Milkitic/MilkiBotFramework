using System;

namespace MilkiBotFramework.Plugins.CommandLine.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class RegexCommandHandlerAttribute : Attribute
{
    public RegexCommandHandlerAttribute(string regex)
    {
        RegexString = regex;
    }

    public string RegexString { get; }
}