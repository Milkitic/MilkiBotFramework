using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContractsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Plugins;
using MilkiBotFramework.Plugins.Loading;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework
{
    public class Bot
    {
        private readonly PluginManager _pluginManager;
        private readonly IContractsManager _contractsManager;
        private readonly ILogger<Bot> _logger;
        private TaskCompletionSource? _connectionTcs;

        public Bot(BotTaskScheduler botTaskScheduler, PluginManager pluginManager, IContractsManager contractsManager, IConnector connector, IDispatcher dispatcher, BotOptions options, ILogger<Bot> logger)
        {
            BotTaskScheduler = botTaskScheduler;
            Connector = connector;
            Dispatcher = dispatcher;
            Options = options;
            _pluginManager = pluginManager;
            _contractsManager = contractsManager;
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
                _contractsManager.Initialize();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error occurs while running");
            }

            _connectionTcs.Task.Wait();
        }

        public async Task RunAsync()
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

                await _pluginManager.InitializeAllPlugins();
                _contractsManager.Initialize();
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Error occurs while running.");
            }

            await _connectionTcs.Task;
        }

        public void Stop()
        {
            Connector.DisconnectAsync().Wait();
            _connectionTcs?.SetResult();
            _connectionTcs = null;
        }

        public async Task StopAsync()
        {
            await Connector.DisconnectAsync();
            _connectionTcs?.SetResult();
            _connectionTcs = null;
        }
    }

    public sealed class BotOptions
    {
        public string CacheImageDir { get; set; }
        public string GifSiclePath { get; set; }
        public string FfMpegPath { get; set; }
    }
}
