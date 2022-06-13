using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;

namespace MilkiBotFramework.Plugining;

[PluginLifetime(PluginLifetime.Scoped)]
public abstract class BasicPlugin : PluginBase, IMessagePlugin
{
    public sealed override PluginType PluginType => PluginType.Basic;
#pragma warning disable CS1998
    public virtual async IAsyncEnumerable<IResponse> OnMessageReceived(MessageContext context) { yield break; }
#pragma warning restore CS1998
    public virtual Task<IResponse?> OnBindingFailed(BindingException bindingException, MessageContext context) => Task.FromResult<IResponse?>(null);
}

[PluginLifetime(PluginLifetime.Scoped)]
public abstract class BasicPlugin<TContext> : PluginBase, IMessagePlugin
where TContext : MessageContext
{
    public sealed override PluginType PluginType => PluginType.Basic;
#pragma warning disable CS1998
    public virtual async IAsyncEnumerable<IResponse> OnMessageReceived(TContext request) { yield break; }
#pragma warning restore CS1998
    public virtual Task<IResponse?> OnBindingFailed(BindingException bindingException, MessageContext context) => Task.FromResult<IResponse?>(null);

    IAsyncEnumerable<IResponse> IMessagePlugin.OnMessageReceived(MessageContext context) =>
        OnMessageReceived((TContext)context);
}