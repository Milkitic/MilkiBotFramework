using System.Reflection;

namespace MilkiBotFramework.Plugining.Loading;

public class AssemblyContext
{
    public Assembly Assembly { get; init; } = null!;
    public IReadOnlyList<PluginInfo> PluginInfos { get; init; } = null!;
    public IReadOnlyList<Type> DbContextTypes { get; init; } = null!;
    public string Version { get; init; } = null!;
    public string? Product { get; init; }
}