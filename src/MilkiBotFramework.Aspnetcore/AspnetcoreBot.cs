using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.Aspnetcore;

public class AspnetcoreBot : Bot
{
    internal WebApplication WebApplication { get; set; }

    public AspnetcoreBot(BotTaskScheduler botTaskScheduler, PluginManager pluginManager, IContactsManager contactsManager, IConnector connector, IDispatcher dispatcher, BotOptions options, ILogger<AspnetcoreBot> logger)
        : base(botTaskScheduler, pluginManager, contactsManager, connector, dispatcher, options, logger)
    {
    }

    public override void Run()
    {
        if (ConnectionTcs != null) throw new InvalidOperationException();
        ConnectionTcs = new TaskCompletionSource();
        try
        {
            try
            {
                WebApplication.StartAsync().Wait(3000);
                //Connector.ConnectAsync().Wait(3000);
            }
            catch
            {
                // ignored
            }

            PluginManager.InitializeAllPlugins().Wait();
            ContactsManager.Initialize();
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, "Error occurs while running");
        }

        ConnectionTcs.Task.Wait();
    }

    public override async Task<int> RunAsync()
    {
        if (ConnectionTcs != null) throw new InvalidOperationException();
        ConnectionTcs = new TaskCompletionSource();
        try
        {
            ExitCode = 0;
            try
            {
                WebApplication.StartAsync().Wait(3000);
                //Connector.ConnectAsync().Wait(3000);
            }
            catch
            {
                // ignored
            }

            await PluginManager.InitializeAllPlugins();
            ContactsManager.Initialize();
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, "Error occurs while running.");
        }

        await ConnectionTcs.Task;
        return ExitCode;
    }

    public override int Stop(int exitCode = 0)
    {
        ExitCode = exitCode;
        Connector.DisconnectAsync().Wait();
        ConnectionTcs?.SetResult();
        ConnectionTcs = null;
        return exitCode;
    }

    public override async Task<int> StopAsync(int exitCode = 0)
    {
        ExitCode = exitCode;
        await Connector.DisconnectAsync();
        ConnectionTcs?.SetResult();
        ConnectionTcs = null;
        return exitCode;
    }
}