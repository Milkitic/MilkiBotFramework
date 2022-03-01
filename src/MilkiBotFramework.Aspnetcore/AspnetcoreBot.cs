using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Aspnetcore;

public class AspnetcoreBot : Bot
{
    internal WebApplication WebApplication { get; set; }

    public AspnetcoreBot(IConnector connector,
        IContactsManager contactsManager,
        IDispatcher dispatcher,
        ILogger<Bot> logger,
        IServiceProvider serviceProvider,
        BotOptions options,
        BotTaskScheduler botTaskScheduler,
        PluginManager pluginManager) : base(connector,
        contactsManager,
        dispatcher,
        logger,
        serviceProvider,
        options,
        botTaskScheduler,
        pluginManager)
    {
    }
}