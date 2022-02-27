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
        protected readonly PluginManager PluginManager;
        protected readonly IContactsManager ContactsManager;
        protected readonly ILogger Logger;
        protected TaskCompletionSource? ConnectionTcs;
        protected int ExitCode;

        public Bot(BotTaskScheduler botTaskScheduler, PluginManager pluginManager, IContactsManager contactsManager, IConnector connector, IDispatcher dispatcher, BotOptions options, ILogger<Bot> logger)
        {
            BotTaskScheduler = botTaskScheduler;
            Connector = connector;
            Dispatcher = dispatcher;
            Options = options;
            PluginManager = pluginManager;
            ContactsManager = contactsManager;
            Logger = logger;
            Current = this;
        }

        public static Bot? Current { get; private set; }

        public BotOptions Options { get; }
        public IServiceProvider? SingletonServiceProvider { get; internal set; }
        public BotTaskScheduler BotTaskScheduler { get; }
        public IConnector Connector { get; }
        public IDispatcher Dispatcher { get; }
        public Action<ILoggingBuilder>? ConfigureLogger { get; set; }

        public virtual void Run()
        {
            if (ConnectionTcs != null) throw new InvalidOperationException();
            ConnectionTcs = new TaskCompletionSource();
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

                PluginManager.InitializeAllPlugins().Wait();
                ContactsManager.Initialize();
            }
            catch (Exception ex)
            {
                Logger.LogCritical(ex, "Error occurs while running");
            }

            ConnectionTcs.Task.Wait();
        }

        public virtual async Task<int> RunAsync()
        {
            if (ConnectionTcs != null) throw new InvalidOperationException();
            ConnectionTcs = new TaskCompletionSource();
            try
            {
                ExitCode = 0;
                try
                {
                    Connector.ConnectAsync().Wait(3000);
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

        public virtual int Stop(int exitCode = 0)
        {
            ExitCode = exitCode;
            Connector.DisconnectAsync().Wait();
            ConnectionTcs?.SetResult();
            ConnectionTcs = null;
            return exitCode;
        }

        public virtual async Task<int> StopAsync(int exitCode = 0)
        {
            ExitCode = exitCode;
            await Connector.DisconnectAsync();
            ConnectionTcs?.SetResult();
            ConnectionTcs = null;
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
