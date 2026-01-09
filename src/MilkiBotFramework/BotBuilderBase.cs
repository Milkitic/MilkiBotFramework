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
        var connector = (IConnector)serviceProvider.GetService(typeof(IConnector))!;
        
        // 如果是聚合连接器，我们需要对子连接器应用配置
        // 但是这里 connector 实例已经创建好了
        // 我们需要一种方法来配置它们。
        // 如果我们遍历所有注册的 IConnector？
        // 如果注册为 AddSingleton<IConnector, DiscordConnector>，GetServices<IConnector> 会返回所有。
        // 如果我们用了聚合器，聚合器也是 IConnector。
        
        var connectors = serviceProvider.GetServices<IConnector>();
        foreach (var c in connectors)
        {
            // 对每个 connector 应用所有匹配的配置器
            foreach (var configurator in _connectorConfigurators)
            {
                if (c is IConnectorConfigurable configurable)
                {
                    configurator(configurable);
                }
            }
        }
        
        // 特别处理：如果 connector 是聚合器，它可能没有暴露子连接器供我们遍历
        // 但是我们在 ConfigServices 中是把具体连接器也注册进去了的。
        // 所以 GetServices<IConnector> 应该能拿到具体的连接器。
        // 除非我们把具体连接器注册为具体类型而不是 IConnector。
    }

    protected virtual void ConfigServices(IServiceCollection serviceCollection)
    {
        var configureLogger = _configureLogger ??= CreateDefaultLoggerConfiguration();
        serviceCollection
            .AddLogging(k => configureLogger(k))
            .AddSingleton(GetOptionInstance())
            .AddSingleton<BotTaskScheduler>()
            .AddSingleton<EventBus>()
            .AddSingleton<LightHttpClient>()
            .AddSingleton<PluginManager>()
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
              foreach(var t in _dispatcherTypes)
              {
                  serviceCollection.AddSingleton(t);
              }
              serviceCollection.AddSingleton<IDispatcher>(sp => 
              {
                  var list = new List<IDispatcher>();
                  foreach(var t in _dispatcherTypes) list.Add((IDispatcher)sp.GetRequiredService(t));
                  return new AggregateDispatcher(list);
              });
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
              foreach(var t in _contactsManagerTypes)
              {
                  serviceCollection.AddSingleton(t);
              }
              serviceCollection.AddSingleton<IContactsManager>(sp => 
              {
                  var list = new List<IContactsManager>();
                  foreach(var t in _contactsManagerTypes) list.Add((IContactsManager)sp.GetRequiredService(t));
                  return new AggregateContactsManager(list);
              });
         }
 
         // Connectors
         if (_connectorTypes.Count == 1)
         {
             serviceCollection.AddSingleton(typeof(IConnector), _connectorTypes[0]);
         }
         else if (_connectorTypes.Count > 1)
         {
             foreach (var type in _connectorTypes)
             {
                 // 注册具体的连接器为它们自己的类型
                 serviceCollection.AddSingleton(type);
             }
             // 注册聚合器作为主要的 IConnector
             serviceCollection.AddSingleton<IConnector>(sp => 
             {
                 var list = new List<IConnector>();
                 foreach(var t in _connectorTypes) list.Add((IConnector)sp.GetRequiredService(t));
                 return new AggregateConnector(list);
             });
         }
 
         // MessageApi
         if (_messageApiTypes.Count > 0)
         {
             if (_messageApiTypes.Count == 1)
             {
                 serviceCollection.AddSingleton(_messageApiTypes[0]);
                 serviceCollection.AddSingleton(typeof(IMessageApi), provider => provider.GetService(_messageApiTypes[0])!);
             }
             else
             {
                 foreach (var type in _messageApiTypes)
                 {
                     serviceCollection.AddSingleton(type); // 注册自身类型
                 }
                 serviceCollection.AddSingleton<IMessageApi>(sp => 
                 {
                     var list = new List<IMessageApi>();
                     foreach(var t in _messageApiTypes) list.Add((IMessageApi)sp.GetRequiredService(t));
                     return new AggregateMessageApi(list);
                 });
             }
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
