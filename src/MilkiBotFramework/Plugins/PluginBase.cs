using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MilkiBotFramework.Dispatching;

namespace MilkiBotFramework.Plugins;

public abstract class PluginBase
{
    public Func<PluginMetadata> GetMetadata { get; internal set; }

    public bool IsInitialized { get; protected set; }

    protected internal PluginManager PluginManager { get; internal set; }

    protected virtual Task OnInitialized() => Task.CompletedTask;
    protected virtual Task OnUninitialized() => Task.CompletedTask;
    protected virtual Task OnExecuting() => Task.CompletedTask;
    protected virtual Task OnExecuted() => Task.CompletedTask;

    protected Task<T> ReadValueAsync<T>(string key)
    {
        throw new NotImplementedException();
    }

    protected Task<T[]> ReadArrayAsync<T>(string key)
    {
        throw new NotImplementedException();
    }

    protected Task WriteValueAsync(string key, object value)
    {
        throw new NotImplementedException();
    }

    protected Task WriteArrayAsync<T>(string key, IEnumerable<T> value)
    {
        throw new NotImplementedException();
    }
}