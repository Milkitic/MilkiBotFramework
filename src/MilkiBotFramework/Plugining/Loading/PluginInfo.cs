using System;
using System.Collections.Generic;

namespace MilkiBotFramework.Plugining.Loading;

public sealed class PluginInfo
{
    public PluginMetadata Metadata { get; init; }
    public Type Type { get; init; }
    public Type BaseType { get; init; }
    public PluginLifetime Lifetime { get; init; }
    public IReadOnlyDictionary<string, CommandInfo> Commands { get; init; }
    public int Index { get; init; }
}