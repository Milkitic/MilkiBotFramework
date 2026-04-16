using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework;

public class Bot
{
    public event Func<MessageContext, Task>? OnMessageReceived;

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
        MessageDispatchNotifier messageDispatchNotifier,
        LightHttpClient lightHttpClient,
        PluginCatalog pluginCatalog)
    {
        MessageApi = messageApi;
        RichMessageConverter = richMessageConverter;
        ServiceProvider = serviceProvider;
        Connector = connector;
        Dispatcher = dispatcher;
        PluginCatalog = pluginCatalog;
        LightHttpClient = lightHttpClient;
        Options = options;
        BotTaskScheduler = botTaskScheduler;
        ContactsManager = contactsManager;
        Logger = logger;
        messageDispatchNotifier.MessageDispatched += async messageContext =>
        {
            if (OnMessageReceived != null) await OnMessageReceived(messageContext);
        };
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
    public LightHttpClient LightHttpClient { get; }
    public PluginCatalog PluginCatalog { get; }

    public int Run()
    {
        if (_connectionTcs != null) throw new InvalidOperationException();
        _connectionTcs = new TaskCompletionSource();
        try
        {
            _exitCode = 0;
            try
            {
                Connector.ConnectAsync(CancellationToken.None).Wait(3000);
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

            PluginCatalog.InitializeAllPlugins().Wait();
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
                using var cts = new CancellationTokenSource(3000);
                await Connector.ConnectAsync(cts.Token);
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

            await PluginCatalog.InitializeAllPlugins();
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