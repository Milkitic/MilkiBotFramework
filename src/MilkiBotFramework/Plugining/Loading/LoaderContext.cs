using System.Runtime.Loader;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace MilkiBotFramework.Plugining.Loading;

public class LoaderContext : IAsyncDisposable
{
    public string Name { get; init; } = null!;
    public bool IsRuntimeContext { get; init; }
    public IReadOnlyDictionary<string, AssemblyContext> AssemblyContexts { get; init; } = null!;

    private readonly Lock _syncLock = new();
    private volatile bool _disposed;

    internal IServiceCollection ServiceCollection { get; init; } = null!;
    internal ILifetimeScope HostLifetimeScope { get; init; } = null!;
    internal ILifetimeScope? LifetimeScope { get; private set; }
    internal IServiceProvider? ServiceProvider { get; private set; }
    internal AssemblyLoadContext? AssemblyLoadContext { get; set; }

    internal IServiceProvider BuildServiceProvider()
    {
        if (ServiceProvider != null) return ServiceProvider;

        lock (_syncLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (ServiceProvider != null) return ServiceProvider;

            LifetimeScope = HostLifetimeScope.BeginLifetimeScope(builder => builder.Populate(ServiceCollection));
            ServiceProvider = new AutofacServiceProvider(LifetimeScope);
            return ServiceProvider;
        }
    }

    public async ValueTask DisposeAsync()
    {
        ILifetimeScope? lifetimeScope;
        AssemblyLoadContext? assemblyLoadContext;

        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            lifetimeScope = LifetimeScope;
            assemblyLoadContext = AssemblyLoadContext;
            LifetimeScope = null;
            ServiceProvider = null;
            AssemblyLoadContext = null;
        }

        if (lifetimeScope is IAsyncDisposable asyncLifetimeScope)
        {
            await asyncLifetimeScope.DisposeAsync();
        }
        else
        {
            lifetimeScope?.Dispose();
        }

        if (assemblyLoadContext is { IsCollectible: true })
        {
            var weakReference = new WeakReference(assemblyLoadContext);
            assemblyLoadContext.Unload();
            await WaitForUnloadAsync(weakReference);
        }
    }

    private static async Task WaitForUnloadAsync(WeakReference weakReference)
    {
        for (var i = 0; i < 8 && weakReference.IsAlive; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Yield();
        }
    }
}