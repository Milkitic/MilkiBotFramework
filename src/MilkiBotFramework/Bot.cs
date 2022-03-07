using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework;

public class Bot
{
    public event Func<DispatchMessageEvent, Task>? OnMessageReceived;

    private int _exitCode;
    private TaskCompletionSource? _connectionTcs;

    public Bot(IConnector connector,
        IContactsManager contactsManager,
        IDispatcher dispatcher,
        ILogger<Bot> logger,
        IMessageApi messageApi,
        IRichMessageConverter richMessageConverter,
        IServiceProvider serviceProvider,
        BotOptions options,
        BotTaskScheduler botTaskScheduler,
        EventBus eventBus,
        LightHttpClient lightHttpClient,
        PluginManager pluginManager)
    {
        MessageApi = messageApi;
        RichMessageConverter = richMessageConverter;
        EventBus = eventBus;
        ServiceProvider = serviceProvider;
        Connector = connector;
        Dispatcher = dispatcher;
        PluginManager = pluginManager;
        LightHttpClient = lightHttpClient;
        Options = options;
        BotTaskScheduler = botTaskScheduler;
        ContactsManager = contactsManager;
        Logger = logger;
        eventBus.Subscribe<DispatchMessageEvent>(async k =>
        {
            if (OnMessageReceived != null) await OnMessageReceived(k);
        });
    }

    public IConnector Connector { get; }
    public IContactsManager ContactsManager { get; }
    public IDispatcher Dispatcher { get; }
    public ILogger Logger { get; }
    public IMessageApi MessageApi { get; }
    public IRichMessageConverter RichMessageConverter { get; }
    public IServiceProvider ServiceProvider { get; }
    public BotOptions Options { get; }
    public BotTaskScheduler BotTaskScheduler { get; }
    public EventBus EventBus { get; }
    public LightHttpClient LightHttpClient { get; }
    public PluginManager PluginManager { get; }

    public int Run()
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

            PluginManager.InitializeAllPlugins().Wait();
            ContactsManager.InitializeTasks();
        }
        catch (Exception ex)
        {
            Logger.LogCritical(ex, "Error occurs while running");
            return ex.HResult;
        }

        _connectionTcs.Task.Wait();
        return _exitCode;
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
            return ex.HResult;
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
    public string PluginHomeDir { get; set; } = "./homes";
    public string PluginDatabaseDir { get; set; } = "./databases";
    public string PluginConfigurationDir { get; set; } = "./configurations";
    public string CacheImageDir { get; set; } = "./caches/images";
    public string GifSiclePath { get; set; }
    public string FfMpegPath { get; set; }
}