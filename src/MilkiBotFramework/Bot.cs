using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework
{
    public class Bot
    {
        private int _exitCode;
        private TaskCompletionSource? _connectionTcs;

        protected readonly IConnector Connector;
        protected readonly IContactsManager ContactsManager;
        protected readonly IDispatcher Dispatcher;
        protected readonly ILogger Logger;
        protected readonly IServiceProvider ServiceProvider;
        protected readonly BotOptions Options;
        protected readonly BotTaskScheduler BotTaskScheduler;
        protected readonly PluginManager PluginManager;

        public Bot(IConnector connector,
            IContactsManager contactsManager,
            IDispatcher dispatcher,
            ILogger<Bot> logger,
            IServiceProvider serviceProvider,
            BotOptions options,
            BotTaskScheduler botTaskScheduler,
            PluginManager pluginManager)
        {
            ServiceProvider = serviceProvider;
            Connector = connector;
            Dispatcher = dispatcher;
            PluginManager = pluginManager;
            Options = options;
            BotTaskScheduler = botTaskScheduler;
            ContactsManager = contactsManager;
            Logger = logger;
        }

        public void Run()
        {
            if (_connectionTcs != null) throw new InvalidOperationException();
            _connectionTcs = new TaskCompletionSource();
            try
            {
                try
                {
                    Connector.ConnectAsync().Wait(3000);
                }
                catch (Exception ex)
                {
                    if (ex is not TaskCanceledException &&
                        ex.InnerException is not TaskCanceledException)
                    {
                        throw;
                    }
                    // ignored
                }

                PluginManager.InitializeAllPlugins().Wait();
                ContactsManager.InitializeTasks();
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Error occurs while running");
            }

            _connectionTcs.Task.Wait();
        }

        public async Task<int> RunAsync()
        {
            if (_connectionTcs != null) throw new InvalidOperationException();
            _connectionTcs = new TaskCompletionSource();
            try
            {
                _exitCode = 0;
                try
                {
                    Connector.ConnectAsync().Wait(3000);
                }
                catch (Exception ex)
                {
                    if (ex is not TaskCanceledException &&
                        ex.InnerException is not TaskCanceledException)
                    {
                        throw;
                    }
                    // ignored
                }

                await PluginManager.InitializeAllPlugins();
                ContactsManager.InitializeTasks();
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Error occurs while running.");
            }

            await _connectionTcs.Task;
            return _exitCode;
        }

        public int Stop(int exitCode = 0)
        {
            _exitCode = exitCode;
            Connector.DisconnectAsync().Wait();
            _connectionTcs?.SetResult();
            _connectionTcs = null;
            return exitCode;
        }

        public async Task<int> StopAsync(int exitCode = 0)
        {
            _exitCode = exitCode;
            await Connector.DisconnectAsync();
            _connectionTcs?.SetResult();
            _connectionTcs = null;
            return exitCode;
        }
    }

    public sealed class BotOptions
    {
        public HashSet<string> RootAccounts { get; set; } = new();
        public string PluginBaseDir { get; set; } = "./plugins";
        public string PluginDatabaseDir { get; set; } = "./databases";
        public string PluginConfigurationDir { get; set; } = "./configurations";
        public string CacheImageDir { get; set; } = "./caches/images";
        public string GifSiclePath { get; set; }
        public string FfMpegPath { get; set; }
    }
}
