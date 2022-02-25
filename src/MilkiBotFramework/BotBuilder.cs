using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContractsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Tasking;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework;

public sealed class BotBuilder
{
    internal BotBuilder()
    {
        _services = new ServiceCollection();
    }

    private readonly BotOptions _botOptions = new();
    internal Action<ILoggingBuilder>? _configureLogger;
    private Action<IConnectorConfigurable>? _configureConnector;
    private Type? _connectorType;
    private Type? _dispatcherType;
    private Type? _messageApiType;
    private Type? _contractsManagerType;
    private readonly ServiceCollection _services;
    private string _pluginBaseDir = "./plugins";
    private Type? _commandAnalyzerType;
    private IParameterConverter? _defaultConverter;
    private Type? _richMessageConverterType;

    public BotBuilder ConfigureOptions(Action<BotOptions> configureBot)
    {
        configureBot?.Invoke(_botOptions);
        return this;
    }

    public BotBuilder ConfigureLogger(Action<ILoggingBuilder> configureLogger)
    {
        _configureLogger = configureLogger;
        return this;
    }

    public BotBuilder SetPluginBaseDirectory(string directory)
    {
        _pluginBaseDir = directory;
        return this;
    }

    public BotBuilder UseConnector<T>(Action<IConnectorConfigurable>? configureConnector = null) where T : IConnector
    {
        _connectorType = typeof(T);
        _configureConnector = configureConnector;
        return this;
    }

    public BotBuilder UseConnector<T>(string uri) where T : IConnector
    {
        return UseConnector<T>(connector => connector.ServerUri = uri);
    }

    public BotBuilder UseContractsManager<T>() where T : IContractsManager
    {
        _contractsManagerType = typeof(T);
        return this;
    }

    public BotBuilder UseCommandLineAnalyzer<T>(IParameterConverter? defaultConverter = null) where T : ICommandLineAnalyzer
    {
        _defaultConverter = defaultConverter;
        _commandAnalyzerType = typeof(T);
        return this;
    }

    public BotBuilder UseRichMessageConverter<T>() where T : IRichMessageConverter
    {
        _richMessageConverterType = typeof(T);
        return this;
    }

    public BotBuilder UseDispatcher<T>() where T : IDispatcher
    {
        _dispatcherType = typeof(T);
        return this;
    }

    public BotBuilder UseMessageApi<T>() where T : IMessageApi
    {
        _messageApiType = typeof(T);
        return this;
    }

    public BotBuilder ConfigureServices(Action<IServiceCollection> configureServices)
    {
        configureServices?.Invoke(_services);
        return this;
    }

    internal Bot GetBotInstance()
    {
        _configureLogger ??= CreateDefaultLoggerConfiguration();
        _services
            .AddLogging(k =>
            {
//#if DEBUG
//                k.AddFilter(o => true);
//#endif
                _configureLogger(k);
            })
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
            .AddSingleton(typeof(IContractsManager),
                _contractsManagerType ?? throw new ArgumentNullException(nameof(IContractsManager),
                    "The IContractsManager implementation is not specified."))
            .AddSingleton<Bot>();
        if (_messageApiType != null)
        {
            _services.AddSingleton(_messageApiType);
            _services.AddSingleton(typeof(IMessageApi), provider => provider.GetService(_messageApiType)!);
        }

        _services.AddSingleton(_services);
        var serviceProvider = _services.BuildServiceProvider();

        // RichMessageConverter
        var richMessageConverter = serviceProvider.GetService<IRichMessageConverter>()!;
    
        // CommandLineAnalyzer
        var commandLineAnalyzer = serviceProvider.GetService<ICommandLineAnalyzer>()!;
        if (_defaultConverter != null) commandLineAnalyzer.DefaultParameterConverter = _defaultConverter;

        // PluginManager
        var pluginManager = serviceProvider.GetService<PluginManager>()!;
        pluginManager.PluginBaseDirectory = _pluginBaseDir;

        // Connector
        var connector = (IConnector)serviceProvider.GetService(typeof(IConnector));
        _configureConnector?.Invoke(connector);

        // Dispatcher
        var dispatcher = serviceProvider.GetService<IDispatcher>()!;

        // ContractsManager
        var contractsManager = serviceProvider.GetService<IContractsManager>();
        if (contractsManager is ContractsManagerBase cmb) cmb.Dispatcher = dispatcher;

        // TaskScheduler
        var taskScheduler = serviceProvider.GetService<BotTaskScheduler>()!;
        taskScheduler.SingletonServiceProvider = serviceProvider;

        // Bot
        var bot = (Bot)serviceProvider.GetService(typeof(Bot));
        bot.SingletonServiceProvider = serviceProvider;
        bot.Builder = this;
        pluginManager.BaseServiceCollection = _services;
        pluginManager.BaseServiceProvider = serviceProvider;
        return bot;
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