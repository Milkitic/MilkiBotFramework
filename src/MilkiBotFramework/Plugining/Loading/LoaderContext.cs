using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;

namespace MilkiBotFramework.Plugining.Loading;

internal class LoaderContext
{
    public string Name { get; init; }
    public bool IsRuntimeContext { get; init; }
    public IServiceCollection ServiceCollection { get; init; }
    public ServiceProvider? ServiceProvider { get; private set; }
    public AssemblyLoadContext AssemblyLoadContext { get; init; }
    public Dictionary<string, AssemblyContext> AssemblyContexts { get; } = new();

    public ServiceProvider BuildServiceProvider()
    {
        if (ServiceProvider != null) return ServiceProvider;
        return this.ServiceProvider = ServiceCollection.BuildServiceProvider();
    }
}