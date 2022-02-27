using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Tasking;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework;

public abstract class BotBuilderBase<TBot, TBuilder> where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
{
    private IServiceCollection? _services;

    private readonly BotOptions _botOptions = new();
    private Action<ILoggingBuilder>? _configureLogger;
    private Action<IConnectorConfigurable>? _configureConnector;
    private Type? _connectorType;
    private Type? _dispatcherType;
    private Type? _messageApiType;
    private Type? _contractsManagerType;
    private string _pluginBaseDir = "./plugins";
    private Type? _commandAnalyzerType;
    private IParameterConverter? _defaultConverter;
    private Type? _richMessageConverterType;

    public TBuilder ConfigureOptions(Action<BotOptions> configureBot)
    {
        configureBot?.Invoke(_botOptions);
        return (TBuilder)this;
    }

    public TBuilder ConfigureLogger(Action<ILoggingBuilder> configureLogger)
    {
        _configureLogger = configureLogger;
        return (TBuilder)this;
    }

    public TBuilder SetPluginBaseDirectory(string directory)
    {
        _pluginBaseDir = directory;
        return (TBuilder)this;
    }

    public TBuilder UseConnector<T>(Action<IConnectorConfigurable>? configureConnector = null) where T : IConnector
    {
        _connectorType = typeof(T);
        _configureConnector = configureConnector;
        return (TBuilder)this;
    }

    public TBuilder UseConnector<T>(string uri) where T : IConnector
    {
        return UseConnector<T>(connector => connector.ServerUri = uri);
    }

    public TBuilder UseContractsManager<T>() where T : IContactsManager
    {
        _contractsManagerType = typeof(T);
        return (TBuilder)this;
    }

    public TBuilder UseCommandLineAnalyzer<T>(IParameterConverter? defaultConverter = null) where T : ICommandLineAnalyzer
    {
        _defaultConverter = defaultConverter;
        _commandAnalyzerType = typeof(T);
        return (TBuilder)this;
    }

    public TBuilder UseRichMessageConverter<T>() where T : IRichMessageConverter
    {
        _richMessageConverterType = typeof(T);
        return (TBuilder)this;
    }

    public TBuilder UseDispatcher<T>() where T : IDispatcher
    {
        _dispatcherType = typeof(T);
        return (TBuilder)this;
    }

    public TBuilder UseMessageApi<T>() where T : IMessageApi
    {
        _messageApiType = typeof(T);
        return (TBuilder)this;
    }

    public TBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        configureServices?.Invoke(GetServiceCollection());
        return (TBuilder)this;
    }

    public TBot Build()
    {
        ConfigServices(GetServiceCollection());
        var serviceProvider = BuildCore(GetServiceCollection());
        ConfigureApp(serviceProvider);
        // PluginManager
        var pluginManager = serviceProvider.GetService<PluginManager>()!;
        pluginManager.PluginBaseDirectory = _pluginBaseDir;

        // Bot
        var bot = (Bot)serviceProvider.GetService(typeof(Bot));
        bot.SingletonServiceProvider = serviceProvider;
        bot.ConfigureLogger = _configureLogger;
        pluginManager.BaseServiceCollection = GetServiceCollection();
        pluginManager.BaseServiceProvider = serviceProvider;
        return (TBot)bot;
    }

    protected virtual IServiceProvider BuildCore(IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        return serviceProvider;
    }

    protected virtual void ConfigureApp(IServiceProvider serviceProvider)
    {
        // RichMessageConverter
        var richMessageConverter = serviceProvider.GetService<IRichMessageConverter>()!;

        // CommandLineAnalyzer
        var commandLineAnalyzer = serviceProvider.GetService<ICommandLineAnalyzer>()!;
        if (_defaultConverter != null) commandLineAnalyzer.DefaultParameterConverter = _defaultConverter;

        // Connector
        var connector = (IConnector)serviceProvider.GetService(typeof(IConnector));
        _configureConnector?.Invoke(connector);

        // Dispatcher
        var dispatcher = serviceProvider.GetService<IDispatcher>()!;
        dispatcher.SingletonServiceProvider = serviceProvider;
        // ContractsManager
        var contractsManager = serviceProvider.GetService<IContactsManager>();
        if (contractsManager is ContactsManagerBase cmb) cmb.Dispatcher = dispatcher;

        // TaskScheduler
        var taskScheduler = serviceProvider.GetService<BotTaskScheduler>()!;
        taskScheduler.SingletonServiceProvider = serviceProvider;
    }

    protected virtual void ConfigServices(IServiceCollection serviceCollection)
    {
        var configureLogger = _configureLogger ??= CreateDefaultLoggerConfiguration();
        serviceCollection
            .AddLogging(k => configureLogger(k))
            .AddSingleton(_botOptions)
            .AddSingleton<ImageProcessor>()
            .AddSingleton<BotTaskScheduler>()
            .AddSingleton<PluginManager>()
            .AddSingleton(typeof(ICommandLineAnalyzer),
                _commandAnalyzerType ?? typeof(CommandLineAnalyzer))
            .AddSingleton(typeof(IRichMessageConverter),
                _richMessageConverterType ?? typeof(DefaultRichMessageConverter))
            .AddSingleton(typeof(IConnector),
                _connectorType ?? throw new ArgumentNullException(nameof(IConnector),
                    "The IConnector implementation is not specified."))
            .AddSingleton(typeof(IDispatcher),
                _dispatcherType ?? throw new ArgumentNullException(nameof(IDispatcher),
                    "The IDispatcher implementation is not specified."))
            .AddSingleton(typeof(IContactsManager),
                _contractsManagerType ?? throw new ArgumentNullException(nameof(IContactsManager),
                    "The IContractsManager implementation is not specified."))
            .AddSingleton(typeof(Bot), typeof(TBot));
        if (_messageApiType != null)
        {
            serviceCollection.AddSingleton(_messageApiType);
            serviceCollection.AddSingleton(typeof(IMessageApi), provider => provider.GetService(_messageApiType)!);
        }

        serviceCollection.AddSingleton(serviceCollection);
    }

    protected virtual IServiceCollection GetServiceCollection()
    {
        return _services ??= new ServiceCollection();
    }

    private static Action<ILoggingBuilder> CreateDefaultLoggerConfiguration()
    {
        return k => k.AddSimpleConsole(options =>
        {
            options.IncludeScopes = true;
            //options.SingleLine = true;
            options.TimestampFormat = "hh:mm:ss.ffzz ";
        });
    }
}