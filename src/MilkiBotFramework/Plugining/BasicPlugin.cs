using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;

namespace MilkiBotFramework.Plugining;

[PluginLifetime(PluginLifetime.Scoped)]
public abstract class BasicPlugin : PluginBase, IMessagePlugin
{
    public sealed override PluginType PluginType => PluginType.Basic;
    public virtual async IAsyncEnumerable<IResponse> OnMessageReceived(MessageContext context) { yield break; }
    public virtual async Task<IResponse?> OnBindingFailed(BindingException bindingException, MessageContext context) => null;
}

[PluginLifetime(PluginLifetime.Scoped)]
public abstract class BasicPlugin<TContext> : PluginBase, IMessagePlugin
    where TContext : MessageContext
{
    public sealed override PluginType PluginType => PluginType.Basic;
    public virtual async IAsyncEnumerable<IResponse> OnMessageReceived(TContext request) { yield break; }
    public virtual async Task<IResponse?> OnBindingFailed(BindingException bindingException, MessageContext context) => null;

    IAsyncEnumerable<IResponse> IMessagePlugin.OnMessageReceived(MessageContext context) =>
        OnMessageReceived((TContext)context);
}