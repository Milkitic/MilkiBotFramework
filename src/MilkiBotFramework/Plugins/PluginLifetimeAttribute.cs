using System;

namespace MilkiBotFramework.Plugins;

[AttributeUsage(AttributeTargets.Class)]
public sealed class PluginLifetimeAttribute : Attribute
{
    public PluginLifetimeAttribute(PluginLifetime lifetime)
    {
        Lifetime = lifetime;
    }

    public PluginLifetime Lifetime { get; }
}

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

[AttributeUsage(AttributeTargets.Class)]
public sealed class AuthorAttribute : Attribute
{
    public AuthorAttribute(params string[] author)
    {
        Author = author;
    }
    public string[] Author { get; }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class VersionAttribute : Attribute
{
    public VersionAttribute(string version)
    {
        Version = version;
    }

    public string Version { get; }
}