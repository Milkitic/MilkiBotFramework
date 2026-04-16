using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.Dispatching;
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
    
    // Connectors
    private readonly List<Type> _connectorTypes = new();
    private readonly List<Action<IConnectorConfigurable>> _connectorConfigurators = new();

    // Message APIs
    private readonly List<Type> _messageApiTypes = new();
    
    private readonly List<Type> _dispatcherTypes = new();
    private readonly List<Type> _contactsManagerTypes = new();

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

    public TBuilder UseConnector<T>(Action<T>? configureConnector = null) where T : IConnector
    {
        if (!_connectorTypes.Contains(typeof(T)))
        {
            _connectorTypes.Add(typeof(T));
        }
        
        _connectorConfigurators.Add(configurable => 
        {
            if (configurable is T t)
            {
                configureConnector?.Invoke(t);
            }
        });
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
        if (!_contactsManagerTypes.Contains(typeof(T)))
        {
            _contactsManagerTypes.Add(typeof(T));
        }
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
        if (!_dispatcherTypes.Contains(typeof(T)))
        {
            _dispatcherTypes.Add(typeof(T));
        }

        return (TBuilder)this;
    }

    public TBuilder UseMessageApi<T>() where T : IMessageApi
    {
        if (!_messageApiTypes.Contains(typeof(T)))
        {
            _messageApiTypes.Add(typeof(T));
        }

        return (TBuilder)this;
    }

    public TBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        if (configureServices == null) throw new ArgumentNullException(nameof(configureServices));
        configureServices.Invoke(GetServiceCollection());
        return (TBuilder)this;
    }

    public TBot Build()
    {
        var serviceCollection = GetServiceCollection();
        ConfigServices(serviceCollection);
        IServiceProvider? serviceProvider = null;
        // ReSharper disable once AccessToModifiedClosure
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
        _ = serviceProvider.GetService<IRichMessageConverter>()!;

        // CommandLineAnalyzer
        var commandLineAnalyzer = serviceProvider.GetService<ICommandLineAnalyzer>()!;
        if (_defaultConverter != null) commandLineAnalyzer.DefaultParameterConverter = _defaultConverter;

        // Connector
        var connectors = serviceProvider.GetServices<IConnector>();
        foreach (var c in connectors)
        {
            foreach (var configurator in _connectorConfigurators)
            {
                if (c is IConnectorConfigurable configurable)
                {
                    configurator(configurable);
                }
            }
        }
    }

    protected virtual void ConfigServices(IServiceCollection serviceCollection)
    {
        var configureLogger = _configureLogger ??= CreateDefaultLoggerConfiguration();
        serviceCollection
            .AddLogging(k => configureLogger(k))
            .AddSingleton(GetOptionInstance())
            .AddSingleton<BotTaskScheduler>()
            .AddSingleton<LightHttpClient>()
            .AddSingleton<PluginCatalog>()
            .AddSingleton<CommandInjector>()
            .AddSingleton<AsyncMessageSessionManager>()
            .AddSingleton<PluginResponseDispatcher>()
            .AddSingleton<PluginRuntime>()
            .AddSingleton<IMessageContextEnricher, MessageContextEnricher>()
            .AddSingleton<MessageDispatchNotifier>()
            .AddSingleton<MessageDispatchCoordinator>()
            .AddSingleton(new ConfigLoggerProvider(_configureLogger))
            .AddSingleton(typeof(ICommandLineAnalyzer),
                _commandAnalyzerType ?? typeof(CommandLineAnalyzer))
            .AddSingleton(typeof(IRichMessageConverter),
                _richMessageConverterType ?? typeof(DefaultRichMessageConverter))
            .AddSingleton(typeof(ConfigurationFactory))
            .AddSingleton(typeof(IConfiguration<>), typeof(Configuration<>))
            .AddTransient(typeof(LoaderContext), _ => null!)
            .AddSingleton(typeof(Bot), typeof(TBot));
            
        // Dispatcher
         if (_dispatcherTypes.Count == 0)
         {
              throw new ArgumentNullException(nameof(IDispatcher), "The IDispatcher implementation is not specified.");
         }
         else if (_dispatcherTypes.Count == 1)
         {
              serviceCollection.AddSingleton(typeof(IDispatcher), _dispatcherTypes[0]);
         }
         else
         {
              throw new InvalidOperationException("Multiple IDispatcher implementations are not supported. Create separate Bot instances for each platform.");
         }
 
         // ContactsManager
         if (_contactsManagerTypes.Count == 0)
         {
              throw new ArgumentNullException(nameof(IContactsManager), "The IContactsManager implementation is not specified.");
         }
         else if (_contactsManagerTypes.Count == 1)
         {
              serviceCollection.AddSingleton(typeof(IContactsManager), _contactsManagerTypes[0]);
         }
         else
         {
              throw new InvalidOperationException("Multiple IContactsManager implementations are not supported. Create separate Bot instances for each platform.");
         }
 
         // Connectors
         if (_connectorTypes.Count == 0)
         {
              throw new ArgumentNullException(nameof(IConnector), "The IConnector implementation is not specified.");
         }
         else if (_connectorTypes.Count == 1)
         {
             serviceCollection.AddSingleton(typeof(IConnector), _connectorTypes[0]);
         }
         else
         {
             throw new InvalidOperationException("Multiple IConnector implementations are not supported. Create separate Bot instances for each platform.");
         }
 
         // MessageApi
         if (_messageApiTypes.Count == 0)
         {
              // IMessageApi is optional, so no exception here.
         }
         else if (_messageApiTypes.Count == 1)
         {
             serviceCollection.AddSingleton(_messageApiTypes[0]);
             serviceCollection.AddSingleton(typeof(IMessageApi), provider => provider.GetService(_messageApiTypes[0])!);
         }
         else
         {
             throw new InvalidOperationException("Multiple IMessageApi implementations are not supported. Create separate Bot instances for each platform.");
         }

        serviceCollection.AddSingleton(serviceCollection);
    }

    protected virtual IServiceCollection GetServiceCollection()
    {
        return _services ??= new ServiceCollection();
    }

    private static Action<ILoggingBuilder> CreateDefaultLoggerConfiguration()
    {
        return logging => logging.AddConsole();
    }
}
