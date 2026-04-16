using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining;

public interface INoticeHandler
{
    Task<IResponse?> OnNoticeReceived(MessageContext messageContext);
}

public interface IResponseInterceptor
{
    Task<bool> BeforeSend(PluginInfo pluginInfo, IResponse response);
    Task AfterSend(PluginInfo pluginInfo, IResponse response);
}

public interface IPluginExceptionHandler
{
    Task<IResponse?> OnPluginException(Exception exception, MessageContext context);
}

public interface IBindingFailureHandler
{
    Task<IResponse?> OnBindingFailed(BindingException bindingException, MessageContext context);
}