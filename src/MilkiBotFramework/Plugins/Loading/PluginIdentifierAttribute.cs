using System;

namespace MilkiBotFramework.Plugins.Loading;

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