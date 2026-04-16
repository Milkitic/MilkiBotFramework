using System.Reflection;
using System.Runtime.Loader;

namespace MilkiBotFramework.Plugining.Loading;

internal sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly IReadOnlyDictionary<string, string> _assemblyPaths;

    public PluginAssemblyLoadContext(string name, IEnumerable<string> assemblyFiles)
        : base(name, isCollectible: true)
    {
        _assemblyPaths = assemblyFiles
            .GroupBy(Path.GetFileNameWithoutExtension, StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrEmpty(group.Key))
            .ToDictionary(group => group.Key!, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var defaultAssembly = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(assembly =>
            !assembly.IsDynamic && AssemblyName.ReferenceMatchesDefinition(assemblyName, assembly.GetName()));
        if (defaultAssembly != null)
        {
            return defaultAssembly;
        }

        if (assemblyName.Name != null && _assemblyPaths.TryGetValue(assemblyName.Name, out var assemblyPath))
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }
}