using System;

namespace MilkiBotFramework.Plugins.CommandLine.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class RegexCommandAttribute : Attribute
{
    public RegexCommandAttribute(string regex, string alias)
    {
        RegexString = regex;
        Alias = alias;
    }

    public string RegexString { get; }
    public string Alias { get; }
}