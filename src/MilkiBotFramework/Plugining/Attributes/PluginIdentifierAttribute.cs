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

    /// <summary>
    /// 插件优先级，越小则优先级越高
    /// </summary>
    public int Index { get; init; }
}