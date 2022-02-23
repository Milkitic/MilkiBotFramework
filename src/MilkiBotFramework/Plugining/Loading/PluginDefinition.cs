using System;

namespace MilkiBotFramework.Plugining.Loading;

public sealed class PluginDefinition
{
    public PluginMetadata Metadata { get; init; }
    public Type Type { get; init; }
    public Type BaseType { get; init; }
    public PluginLifetime Lifetime { get; init; }
}