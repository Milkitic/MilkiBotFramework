using System.Reflection;

namespace MilkiBotFramework.Plugining.Loading;

internal class AssemblyContext
{
    public string AssemblyName { get; init; }
    public Assembly Assembly { get; init; }
    public List<PluginInfo> PluginInfos { get; } = new();
    public List<Type> DbContextTypes { get; } = new();
}