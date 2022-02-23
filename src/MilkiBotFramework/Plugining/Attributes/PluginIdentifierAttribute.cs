using System;

namespace MilkiBotFramework.Plugining.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class PluginIdentifierAttribute : Attribute
{
    public PluginIdentifierAttribute(string guid, string? name = null)
    {
        Guid = guid;
        Name = name;
    }

    public string Guid { get; }
    public string? Name { get; }
}