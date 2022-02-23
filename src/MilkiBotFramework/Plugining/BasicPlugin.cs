using System.Threading.Tasks;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;

namespace MilkiBotFramework.Plugining;

[PluginLifetime(PluginLifetime.Scoped)]
public abstract class BasicPlugin : PluginBase, IMessagePlugin
{
    public sealed override PluginType PluginType => PluginType.Basic;
    public Task OnMessageReceived(MessageRequestContext request, MessageResponseContext response) =>
        Task.CompletedTask;
}

[PluginLifetime(PluginLifetime.Scoped)]
public abstract class BasicPlugin<TRequestContext> : PluginBase, IMessagePlugin
    where TRequestContext : MessageRequestContext
{
    public sealed override PluginType PluginType => PluginType.Basic;
    public virtual Task OnMessageReceived(TRequestContext request, MessageResponseContext response) =>
        Task.CompletedTask;
    Task IMessagePlugin.OnMessageReceived(MessageRequestContext request, MessageResponseContext response) =>
        OnMessageReceived((TRequestContext)request, response);
}