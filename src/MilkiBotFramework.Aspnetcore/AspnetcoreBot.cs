using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Aspnetcore;

public class AspnetcoreBot : Bot
{
    internal WebApplication WebApplication { get; set; }
    internal AspnetcoreConnector AspnetcoreConnector { get; set; }

    public AspnetcoreBot(BotTaskScheduler botTaskScheduler, PluginManager pluginManager, IContactsManager contactsManager, IConnector connector, IDispatcher dispatcher, BotOptions options, ILogger<AspnetcoreBot> logger)
        : base(botTaskScheduler, pluginManager, contactsManager, connector, dispatcher, options, logger)
    {
        AspnetcoreConnector = (AspnetcoreConnector)connector;
    }
}