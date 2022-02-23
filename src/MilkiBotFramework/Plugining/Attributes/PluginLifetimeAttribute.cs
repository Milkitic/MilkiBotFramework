using System;

namespace MilkiBotFramework.Plugining.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public sealed class PluginLifetimeAttribute : Attribute
{
    public PluginLifetimeAttribute(PluginLifetime lifetime)
    {
        Lifetime = lifetime;
    }

    public PluginLifetime Lifetime { get; }
}