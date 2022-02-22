using System.Threading.Tasks;
using MilkiBotFramework.Messaging;

namespace MilkiBotFramework.Plugins;

[PluginLifetime(PluginLifetime.Scoped)]
public abstract class BasicPlugin : PluginBase, IMessagePlugin
{
    public Task OnMessageReceived(MessageRequestContext request, MessageResponseContext response) =>
        Task.CompletedTask;
}

[PluginLifetime(PluginLifetime.Scoped)]
public abstract class BasicPlugin<TRequestContext> : PluginBase, IMessagePlugin
    where TRequestContext : MessageRequestContext
{
    public virtual Task OnMessageReceived(TRequestContext request, MessageResponseContext response) =>
        Task.CompletedTask;
    Task IMessagePlugin.OnMessageReceived(MessageRequestContext request, MessageResponseContext response) =>
        OnMessageReceived((TRequestContext)request, response);
}

[PluginLifetime(PluginLifetime.Singleton)]
public abstract class ServicePlugin : PluginBase
{
}
