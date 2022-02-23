using System;

namespace MilkiBotFramework.Plugins.Loading;

[AttributeUsage(AttributeTargets.Class)]
public sealed class VersionAttribute : Attribute
{
    public VersionAttribute(string version)
    {
        Version = version;
    }

    public string Version { get; }
}