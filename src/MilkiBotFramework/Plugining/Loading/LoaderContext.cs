using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;

namespace MilkiBotFramework.Plugining.Loading;

public class LoaderContext
{
    public string Name { get; init; }
    public bool IsRuntimeContext { get; init; }
    public IReadOnlyDictionary<string, AssemblyContext> AssemblyContexts { get; init; }

    internal IServiceCollection ServiceCollection { get; init; }
    internal ServiceProvider? ServiceProvider { get; private set; }
    internal AssemblyLoadContext AssemblyLoadContext { get; init; }

    internal ServiceProvider BuildServiceProvider()
    {
        if (ServiceProvider != null) return ServiceProvider;
        return this.ServiceProvider = ServiceCollection.BuildServiceProvider();
    }
}