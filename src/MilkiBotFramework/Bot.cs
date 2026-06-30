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
    private static readonly TimeSpan ConnectorStartupTimeout = TimeSpan.FromSeconds(30);

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
        Connector.MessageReceived += Dispatcher.InvokeMessageReceived;
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

    public async Task<int> RunAsync()
    {
        if (_connectionTcs != null) throw new InvalidOperationException();
        _connectionTcs = new TaskCompletionSource();
        try
        {
            _exitCode = 0;
            try
            {
                // Connector startup may include remote auth calls before the local server starts.
                using var cts = new CancellationTokenSource(ConnectorStartupTimeout);
                await Connector.ConnectAsync(cts.Token);
            }
            catch (Exception ex)
            {
                if (ex is not OperationCanceledException &&
                    ex.InnerException is not OperationCanceledException)
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

    public async Task<int> StopAsync(int exitCode = 0)
    {
        _exitCode = exitCode;
        await Connector.DisconnectAsync();
        await PluginCatalog.DisposeAsync();
        _connectionTcs?.SetResult();
        _connectionTcs = null;
        return exitCode;
    }

    public void ReloadPlugins()
    {
        PluginCatalog.ReloadAllPluginsAsync().Wait();
    }

    public Task ReloadPluginsAsync()
    {
        return PluginCatalog.ReloadAllPluginsAsync();
    }
}
