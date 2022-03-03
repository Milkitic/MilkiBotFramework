using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining;

[PluginLifetime(PluginLifetime.Singleton)]
public abstract class ServicePlugin : PluginBase
{
    public sealed override PluginType PluginType => PluginType.Service;
    public virtual Task<bool> BeforeSend(PluginInfo pluginInfo, IResponse response) => Task.FromResult(true);
    public virtual Task<IResponse?> OnNoticeReceived(MessageContext messageContext) => Task.FromResult<IResponse?>(default);
    public virtual Task<IResponse?> OnPluginException(MessageContext messageContext) => Task.FromResult<IResponse?>(default);
    public virtual Task<IResponse?> OnBindingFailed(BindingException bindingException, MessageContext context) => Task.FromResult<IResponse?>(default);
}