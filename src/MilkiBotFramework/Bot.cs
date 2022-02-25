using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework
{
    public class Bot
    {
        private readonly PluginManager _pluginManager;
        private readonly IContactsManager _contactsManager;
        private readonly ILogger<Bot> _logger;
        private TaskCompletionSource? _connectionTcs;
        private int _exitCode;

        public Bot(BotTaskScheduler botTaskScheduler, PluginManager pluginManager, IContactsManager contactsManager, IConnector connector, IDispatcher dispatcher, BotOptions options, ILogger<Bot> logger)
        {
            BotTaskScheduler = botTaskScheduler;
            Connector = connector;
            Dispatcher = dispatcher;
            Options = options;
            _pluginManager = pluginManager;
            _contactsManager = contactsManager;
            _logger = logger;
            Current = this;
        }

        public static Bot? Current { get; private set; }

        public BotOptions Options { get; }
        public IServiceProvider? SingletonServiceProvider { get; internal set; }
        public BotTaskScheduler BotTaskScheduler { get; }
        public IConnector Connector { get; }
        public IDispatcher Dispatcher { get; }
        internal BotBuilder Builder { get; set; }

        public static Bot Create(Action<BotBuilder>? configureBot = null)
        {
            var builder = new BotBuilder();
            configureBot?.Invoke(builder);
            return builder.GetBotInstance();
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
                catch
                {
                    // ignored
                }

                _pluginManager.InitializeAllPlugins().Wait();
                _contactsManager.Initialize();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error occurs while running");
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
                catch
                {
                    // ignored
                }

                await _pluginManager.InitializeAllPlugins();
                _contactsManager.Initialize();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error occurs while running.");
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
        public string CacheImageDir { get; set; } = "./caches/images";
        public string GifSiclePath { get; set; }
        public string FfMpegPath { get; set; }
    }
}
