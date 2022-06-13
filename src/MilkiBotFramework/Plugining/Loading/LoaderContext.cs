using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;

namespace MilkiBotFramework.Plugining.Loading;

public class LoaderContext
{
    public string Name { get; init; } = null!;
    public bool IsRuntimeContext { get; init; }
    public IReadOnlyDictionary<string, AssemblyContext> AssemblyContexts { get; init; } = null!;

    internal IServiceCollection ServiceCollection { get; init; } = null!;
    internal ServiceProvider? ServiceProvider { get; private set; }
    internal AssemblyLoadContext AssemblyLoadContext { get; init; } = null!;

    internal ServiceProvider BuildServiceProvider()
    {
        if (ServiceProvider != null) return ServiceProvider;
        return this.ServiceProvider = ServiceCollection.BuildServiceProvider();
    }
}