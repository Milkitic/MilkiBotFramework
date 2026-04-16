using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining;

[PluginLifetime(PluginLifetime.Singleton)]
public abstract class ServicePlugin : PluginBase,
    IResponseInterceptor,
    INoticeHandler,
    IPluginExceptionHandler,
    IBindingFailureHandler
{
    public sealed override PluginType PluginType => PluginType.Service;
    public virtual Task<bool> BeforeSend(PluginInfo pluginInfo, IResponse response) => Task.FromResult(true);
    public virtual Task AfterSend(PluginInfo pluginInfo, IResponse response) => Task.CompletedTask;
    public virtual Task<IResponse?> OnNoticeReceived(MessageContext messageContext) => Task.FromResult<IResponse?>(default);
    public virtual Task<IResponse?> OnPluginException(Exception exception, MessageContext context) => Task.FromResult<IResponse?>(default);
    public virtual Task<IResponse?> OnBindingFailed(BindingException bindingException, MessageContext context) => Task.FromResult<IResponse?>(default);
}