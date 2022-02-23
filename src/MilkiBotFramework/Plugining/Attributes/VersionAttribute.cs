using System;

namespace MilkiBotFramework.Plugining.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class VersionAttribute : Attribute
{
    public VersionAttribute(string version)
    {
        Version = version;
    }

    public string Version { get; }
}