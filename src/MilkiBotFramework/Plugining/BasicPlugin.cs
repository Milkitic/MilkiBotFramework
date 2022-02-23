using System.Threading.Tasks;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;

namespace MilkiBotFramework.Plugining;

[PluginLifetime(PluginLifetime.Scoped)]
public abstract class BasicPlugin : PluginBase, IMessagePlugin
{
    public sealed override PluginType PluginType => PluginType.Basic;
    public virtual Task OnMessageReceived(MessageContext context) =>
        Task.CompletedTask;
}

[PluginLifetime(PluginLifetime.Scoped)]
public abstract class BasicPlugin<TContext> : PluginBase, IMessagePlugin
    where TContext : MessageContext
{
    public sealed override PluginType PluginType => PluginType.Basic;
    public virtual Task OnMessageReceived(TContext request) =>
        Task.CompletedTask;
    Task IMessagePlugin.OnMessageReceived(MessageContext context) =>
        OnMessageReceived((TContext)context);
}