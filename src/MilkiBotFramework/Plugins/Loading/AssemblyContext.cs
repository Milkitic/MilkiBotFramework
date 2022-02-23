using System.Collections.Generic;
using System.Reflection;

namespace MilkiBotFramework.Plugins.Loading;

internal class AssemblyContext
{
    public string AssemblyName { get; init; }
    public Assembly Assembly { get; init; }
    public List<PluginDefinition> PluginDefinitions { get; } = new();
}