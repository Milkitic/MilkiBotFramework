using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Configuration;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework;

public abstract class BotBuilderBase<TBot, TBuilder> where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
{
    private IServiceCollection? _services;

    private BotOptions? _botOptions;
    private Action<ILoggingBuilder>? _configureLogger;
    private Action<LightHttpClientCreationOptions>? _configureHttp;
    private Action<IConnectorConfigurable>? _configureConnector;
    private Type? _connectorType;
    private Type? _dispatcherType;
    private Type? _messageApiType;
    private Type? _contactsManagerType;
    private Type? _commandAnalyzerType;
    private IParameterConverter? _defaultConverter;
    private Type? _richMessageConverterType;

    private string? _optionPath;
    private Type? _optionType;

    /// <summary>
    /// Should call after UseOptions()
    /// </summary>
    /// <returns></returns>
    public BotOptions GetOptionInstance()
    {
        var path = _optionPath ?? "appsettings.yaml";
        var optionType = _optionType ?? typeof(BotOptions);
        if (_botOptions == null)
        {
            var success = ConfigurationFactory.TryLoadConfigFromFile(optionType, path, new YamlConverter(), null,
                out var config, out var ex);
            if (!success) throw ex!;
            _botOptions = (BotOptions?)config!;
        }

        return _botOptions;
    }

    public TBuilder UseOptions<T>(string? optionPath) where T : BotOptions
    {
        _optionPath = optionPath;
        _optionType = typeof(T);
        return (TBuilder)this;
    }

    public TBuilder ConfigureLogger(Action<ILoggingBuilder> configureLogger)
    {
        _configureLogger = configureLogger;
        return (TBuilder)this;
    }

    public TBuilder ConfigureHttpClient(Action<LightHttpClientCreationOptions> configureHttp)
    {
        _configureHttp = configureHttp;
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
        return UseConnector<T>(connector =>
        {
            connector.TargetUri = uri;
            connector.BindingPath = uri;
        });
    }

    public TBuilder UseContactsManager<T>() where T : IContactsManager
    {
        _contactsManagerType = typeof(T);
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
        var serviceCollection = GetServiceCollection();
        ConfigServices(serviceCollection);
        IServiceProvider? serviceProvider = null;
        serviceCollection.AddSingleton(typeof(IServiceProvider), _ => serviceProvider!);
        serviceProvider = BuildCore(serviceCollection);
        ConfigureApp(serviceProvider);

        // Bot
        var bot = (Bot)serviceProvider.GetService(typeof(Bot))!;
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
    }

    protected virtual void ConfigServices(IServiceCollection serviceCollection)
    {
        var configureLogger = _configureLogger ??= CreateDefaultLoggerConfiguration();
        var configureHttp = _configureHttp ??= CreateDefaultHttpConfiguration();
        var httpOptions = new LightHttpClientCreationOptions();
        configureHttp(httpOptions);
        serviceCollection
            .AddLogging(k => configureLogger(k))
            .AddSingleton(GetOptionInstance())
            .AddSingleton(httpOptions)
            .AddSingleton<BotTaskScheduler>()
            .AddSingleton<EventBus>()
            .AddSingleton<LightHttpClient>()
            .AddSingleton<PluginManager>()
            .AddSingleton(new ConfigLoggerProvider(_configureLogger))
            .AddSingleton(typeof(ICommandLineAnalyzer),
                _commandAnalyzerType ?? typeof(CommandLineAnalyzer))
            .AddSingleton(typeof(IRichMessageConverter),
                _richMessageConverterType ?? typeof(DefaultRichMessageConverter))

            .AddSingleton(typeof(IDispatcher),
                _dispatcherType ?? throw new ArgumentNullException(nameof(IDispatcher),
                    "The IDispatcher implementation is not specified."))
            .AddSingleton(typeof(IContactsManager),
                _contactsManagerType ?? throw new ArgumentNullException(nameof(IContactsManager),
                    "The IContactsManager implementation is not specified."))
            .AddSingleton(typeof(Bot), typeof(TBot));
        if (_connectorType != null)
        {
            serviceCollection.AddSingleton(typeof(IConnector), _connectorType);
        }

        if (_messageApiType != null)
        {
            serviceCollection.AddSingleton(_messageApiType);
            serviceCollection.AddSingleton(typeof(IMessageApi), provider => provider.GetService(_messageApiType)!);
        }

        serviceCollection.AddSingleton(serviceCollection);
    }

    private Action<LightHttpClientCreationOptions> CreateDefaultHttpConfiguration()
    {
        return _ => { };
    }

    protected virtual IServiceCollection GetServiceCollection()
    {
        return _services ??= new ServiceCollection();
    }

    private static Action<ILoggingBuilder> CreateDefaultLoggerConfiguration()
    {
        return builder => builder
            .AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                //options.SingleLine = true;
                options.TimestampFormat = "hh:mm:ss.ffzz ";
            })
            .AddFilter((ns, level) =>
            {
#if !DEBUG
                if (ns.StartsWith("Microsoft") && level < LogLevel.Warning)
                    return false;
                if (level < LogLevel.Information)
                    return false;
                return true;
#else
                if (ns.StartsWith("Microsoft") && level < LogLevel.Information)
                    return false;
                return true;
#endif
            });
    }
}