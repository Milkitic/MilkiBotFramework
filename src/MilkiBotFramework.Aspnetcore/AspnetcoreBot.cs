using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Aspnetcore;

public class AspnetcoreBot : Bot
{
    private readonly WebApplication _webApplication;

    public AspnetcoreBot(IConnector connector,
        IContactsManager contactsManager,
        IDispatcher dispatcher,
        ILogger<Bot> logger,
        IServiceProvider serviceProvider,
        BotOptions options,
        BotTaskScheduler botTaskScheduler,
        PluginManager pluginManager,
        WebApplication webApplication) : base(connector,
        contactsManager,
        dispatcher,
        logger,
        serviceProvider,
        options,
        botTaskScheduler,
        pluginManager)
    {
        _webApplication = webApplication;
    }
}