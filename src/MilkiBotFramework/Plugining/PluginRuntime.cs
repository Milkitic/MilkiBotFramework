using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Configuration;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining;

public sealed class PluginRuntime
{
    private readonly ICommandLineAnalyzer _commandLineAnalyzer;
    private readonly ILogger<PluginRuntime> _logger;
    private readonly AsyncMessageSessionManager _asyncMessageSessionManager;
    private readonly CommandInjector _commandInjector;
    private readonly PluginCatalog _pluginCatalog;
    private readonly PluginResponseDispatcher _responseDispatcher;
    private readonly PluginSwitchingConfiguration _pluginSwitchingConfiguration;
    private readonly Lock _executionCacheLock = new();

    private volatile ExecutionCache? _executionCache;

    public PluginRuntime(ICommandLineAnalyzer commandLineAnalyzer,
        ILogger<PluginRuntime> logger,
        AsyncMessageSessionManager asyncMessageSessionManager,
        CommandInjector commandInjector,
        PluginCatalog pluginCatalog,
        PluginResponseDispatcher responseDispatcher,
        IConfiguration<PluginSwitchingConfiguration> pluginSwitchingConfiguration)
    {
        _commandLineAnalyzer = commandLineAnalyzer;
        _logger = logger;
        _asyncMessageSessionManager = asyncMessageSessionManager;
        _commandInjector = commandInjector;
        _pluginCatalog = pluginCatalog;
        _responseDispatcher = responseDispatcher;
        _pluginSwitchingConfiguration = pluginSwitchingConfiguration.Instance;
        _pluginCatalog.ExecutionPlanChanged += InvalidateExecutionCache;
    }

    public async Task HandleMessageAsync(MessageContext messageContext)
    {
        var messageType = messageContext.MessageIdentity?.MessageType;
        if (messageType is MessageType.Private or MessageType.Channel)
        {
            await HandleTextMessageAsync(messageContext);
            return;
        }

        await HandleNoticeMessageAsync(messageContext);
    }

    private async Task HandleNoticeMessageAsync(MessageContext messageContext)
    {
        await using var executionContext = await CreateExecutionContextAsync(includeBasicPlugins: false,
            messageContext.MessageIdentity);
        foreach (var noticeHandler in executionContext.NoticeHandlers)
        {
            var response = await noticeHandler.Hook.OnNoticeReceived(messageContext);
            var handled = await DispatchResponseAsync(messageContext,
                noticeHandler.PluginInfo,
                response,
                executionContext,
                allowAsyncSession: false);
            if (handled)
            {
                break;
            }
        }
    }

    private async Task HandleTextMessageAsync(MessageContext messageContext)
    {
        if (_asyncMessageSessionManager.TryConsume(messageContext))
        {
            return;
        }

        await using var executionContext = await CreateExecutionContextAsync(includeBasicPlugins: true,
            messageContext.MessageIdentity);

        var message = messageContext.TextMessage;
        string? commandName;
        CommandLineResult? commandLineResult = messageContext.CommandLineResult;
        if (commandLineResult != null)
        {
            commandName = commandLineResult.Command?.ToString();
        }
        else if (message != null)
        {
            commandName = null;
            var success = _commandLineAnalyzer.TryAnalyze(message, out commandLineResult, out var exception);
            if (success)
            {
                commandName = commandLineResult?.Command.ToString();
                messageContext.CommandLineResult = commandLineResult!;
            }
            else if (exception != null)
            {
                _logger.LogWarning("Error occurs while analyzing command: " + exception.Message);
            }
        }
        else
        {
            commandName = null;
        }

        var remainingPlugins = executionContext.BasicPlugins
            .Select(pluginExecution => pluginExecution.PluginInfo)
            .ToHashSet();

        if (commandName != null)
        {
            var disabledCommandPlugin = GetDisabledCommandPlugin(messageContext.MessageIdentity, commandName);
            if (disabledCommandPlugin != null &&
                executionContext.BasicPlugins.All(k => !k.PluginInfo.Commands.ContainsKey(commandName)))
            {
                var disabledText = messageContext.MessageIdentity?.MessageType == MessageType.Private
                    ? "你已禁用此命令。"
                    : "本会话已禁用此命令。";
                await DispatchResponseAsync(messageContext,
                    disabledCommandPlugin,
                    new MessageResponse(new Text(disabledText)) { IsHandled = true },
                    executionContext,
                    allowAsyncSession: false);
                return;
            }
        }

        foreach (var pluginExecution in executionContext.BasicPlugins)
        {
            var pluginInstance = pluginExecution.PluginInstance;
            var pluginInfo = pluginExecution.PluginInfo;
            var serviceProvider = pluginExecution.BasedServiceScope.ServiceProvider;

            if (!remainingPlugins.Remove(pluginInfo))
            {
                continue;
            }

            try
            {
                await pluginInstance.OnExecuting();
                if (commandName != null && pluginInfo.Commands.TryGetValue(commandName, out var commandInfo))
                {
                    try
                    {
                        var responses = _commandInjector.InjectParametersAndRunAsync(commandLineResult!,
                            commandInfo,
                            pluginInstance,
                            messageContext,
                            serviceProvider);
                        await foreach (var response in responses)
                        {
                            if (response is { IsForced: null }) response.Forced();
                            var handled = await DispatchResponseAsync(messageContext,
                                pluginInfo,
                                response,
                                executionContext,
                                allowAsyncSession: true);
                            if (handled)
                            {
                                return;
                            }
                        }
                    }
                    catch (BindingException ex)
                    {
                        var errMsg =
                            $"Command binding failed ({ex.BindingFailureType}; /{ex.BindingSource.CommandInfo.Command}";
                        if (ex.BindingSource.ParameterInfo != null)
                            errMsg +=
                                $".{(ex.BindingSource.ParameterInfo.Name ?? ex.BindingSource.ParameterInfo.ParameterName)}";
                        errMsg += "). Message: " + ex.Message;
                        _logger.LogWarning(errMsg);

                        var response = await ((IMessagePlugin)pluginInstance).OnBindingFailed(ex, messageContext);
                        if (response == null)
                        {
                            foreach (var bindingFailureHandler in executionContext.BindingFailureHandlers)
                            {
                                var fallbackResponse =
                                    await bindingFailureHandler.Hook.OnBindingFailed(ex, messageContext);
                                var handled = await DispatchResponseAsync(messageContext,
                                    pluginInfo,
                                    fallbackResponse,
                                    executionContext,
                                    allowAsyncSession: true);
                                if (handled)
                                {
                                    return;
                                }
                            }
                        }
                        else
                        {
                            var handled = await DispatchResponseAsync(messageContext,
                                pluginInfo,
                                response,
                                executionContext,
                                allowAsyncSession: true);
                            if (handled)
                            {
                                return;
                            }
                        }
                    }
                }
                else
                {
                    var responses = ((IMessagePlugin)pluginInstance).OnMessageReceived(messageContext);
                    await foreach (var response in responses)
                    {
                        var handled = await DispatchResponseAsync(messageContext,
                            pluginInfo,
                            response,
                            executionContext,
                            allowAsyncSession: true);
                        if (handled)
                        {
                            return;
                        }
                    }
                }

                await pluginInstance.OnExecuted();
            }
            catch (Exception ex)
            {
                if (ex is AsyncMessageTimeoutException timeoutException)
                {
                    _asyncMessageSessionManager.Clear(messageContext.MessageUserIdentity);
                    _logger.LogWarning(timeoutException.Message + ": " + pluginInfo.Metadata.Name);
                }
                else
                {
                    foreach (var pluginExceptionHandler in executionContext.PluginExceptionHandlers)
                    {
                        var response =
                            await pluginExceptionHandler.Hook.OnPluginException(ex.InnerException ?? ex, messageContext);
                        var handled = await DispatchResponseAsync(messageContext,
                            pluginInfo,
                            response,
                            executionContext,
                            allowAsyncSession: false);
                        if (handled)
                        {
                            return;
                        }
                    }

                    using var scope = _logger.BeginScope("{PluginName}", pluginInfo.Metadata.Name);
                    _logger.LogError(ex, "Error Occurs while executing plugin: {MetadataName}. User input: {Message}",
                        pluginInfo.Metadata.Name, message);
                    foreach (var dictionaryEntry in ex.Data.Cast<System.Collections.DictionaryEntry>())
                    {
                        _logger.LogError("Exception Data [{DictionaryEntryKey}]: {DictionaryEntryValue}",
                            dictionaryEntry.Key,
                            dictionaryEntry.Value);
                    }
                }
            }
        }
    }

    private async Task<bool> DispatchResponseAsync(MessageContext messageContext,
        PluginInfo pluginInfo,
        IResponse? response,
        PluginExecutionContext executionContext,
        bool allowAsyncSession)
    {
        if (response == null)
        {
            return false;
        }

        if (response is MessageResponse messageResponse)
        {
            messageResponse.MessageContext = messageContext;
        }

        try
        {
            foreach (var responseInterceptor in executionContext.ResponseInterceptors)
            {
                var shouldContinue = await responseInterceptor.Hook.BeforeSend(pluginInfo, response);
                if (!shouldContinue)
                {
                    response.IsHandled = true;
                    return true;
                }
            }

            if (!response.IsHandled &&
                allowAsyncSession &&
                response.AsyncMessage is AsyncMessage asyncMessage &&
                messageContext.MessageUserIdentity != null)
            {
                _asyncMessageSessionManager.Register(messageContext.MessageUserIdentity, asyncMessage);
            }

            if (response.Message != null)
            {
                await _responseDispatcher.DispatchAsync(messageContext, response);

                foreach (var responseInterceptor in executionContext.ResponseInterceptors)
                {
                    await responseInterceptor.Hook.AfterSend(pluginInfo, response);
                }
            }

            return response.IsHandled;
        }
        finally
        {
            if (response.Message is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (response.Message is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
        }
    }

    private async Task<PluginExecutionContext> CreateExecutionContextAsync(bool includeBasicPlugins,
        MessageIdentity? messageIdentity)
    {
        var executionCache = _pluginCatalog.IsInitialized
            ? GetOrCreateExecutionCache()
            : BuildExecutionCache();
        var noticeHandlers = FilterHooks(executionCache.NoticeHandlers, messageIdentity);
        var responseInterceptors = FilterHooks(executionCache.ResponseInterceptors, messageIdentity);
        var bindingFailureHandlers = FilterHooks(executionCache.BindingFailureHandlers, messageIdentity);
        var pluginExceptionHandlers = FilterHooks(executionCache.PluginExceptionHandlers, messageIdentity);

        if (!includeBasicPlugins)
        {
            return new PluginExecutionContext(Array.Empty<IServiceScope>(),
                Array.Empty<PluginExecutionInfo>(),
                noticeHandlers,
                responseInterceptors,
                bindingFailureHandlers,
                pluginExceptionHandlers);
        }

        var scopesByLoader = new Dictionary<LoaderContext, IServiceScope>();
        var scopes = new List<IServiceScope>();
        var basicPlugins = new List<PluginExecutionInfo>();

        foreach (var descriptor in executionCache.BasicDescriptors)
        {
            if (!IsPluginEnabled(messageIdentity, descriptor.PluginInfo))
            {
                continue;
            }

            var scope = GetOrCreateScope(descriptor.LoaderContext, scopesByLoader, scopes);
            await AddBasicPluginAsync(descriptor.PluginInfo, scope, basicPlugins);
        }

        return new PluginExecutionContext(scopes,
            basicPlugins,
            noticeHandlers,
            responseInterceptors,
            bindingFailureHandlers,
            pluginExceptionHandlers);
    }

    private PluginInfo? GetDisabledCommandPlugin(MessageIdentity? messageIdentity, string commandName)
    {
        var executionCache = _pluginCatalog.IsInitialized
            ? GetOrCreateExecutionCache()
            : BuildExecutionCache();

        foreach (var descriptor in executionCache.BasicDescriptors)
        {
            var pluginInfo = descriptor.PluginInfo;
            if (!pluginInfo.Commands.ContainsKey(commandName))
            {
                continue;
            }

            if (!IsPluginEnabled(messageIdentity, pluginInfo))
            {
                return pluginInfo;
            }
        }

        return null;
    }

    private IReadOnlyList<PluginHook<THook>> FilterHooks<THook>(IEnumerable<PluginHook<THook>> hooks,
        MessageIdentity? messageIdentity)
        where THook : class
    {
        return hooks.Where(hook => IsPluginEnabled(messageIdentity, hook.PluginInfo)).ToArray();
    }

    private bool IsPluginEnabled(MessageIdentity? messageIdentity, PluginInfo pluginInfo)
    {
        return _pluginSwitchingConfiguration.IsPluginEnabled(messageIdentity, pluginInfo);
    }

    private async Task AddBasicPluginAsync(PluginInfo pluginInfo,
        IServiceScope scope,
        ICollection<PluginExecutionInfo> basicPlugins)
    {
        var pluginInstance = ResolvePluginInstance(scope.ServiceProvider, pluginInfo)!;
        if (pluginInfo.Lifetime != PluginLifetime.Singleton)
        {
            try
            {
                await _pluginCatalog.InitializePluginAsync(pluginInstance, pluginInfo);
                basicPlugins.Add(new PluginExecutionInfo(pluginInstance, pluginInfo, true, scope));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurs while initializing plugin: " + pluginInfo.Metadata.Name);
            }
        }
        else
        {
            basicPlugins.Add(new PluginExecutionInfo(pluginInstance, pluginInfo, false, scope));
        }
    }

    private static IServiceScope GetOrCreateScope(LoaderContext loaderContext,
        IDictionary<LoaderContext, IServiceScope> scopesByLoader,
        ICollection<IServiceScope> scopes)
    {
        if (scopesByLoader.TryGetValue(loaderContext, out var scope))
        {
            return scope;
        }

        scope = loaderContext.BuildServiceProvider().CreateScope();
        scopesByLoader.Add(loaderContext, scope);
        scopes.Add(scope);
        return scope;
    }

    private void InvalidateExecutionCache()
    {
        lock (_executionCacheLock)
        {
            _executionCache = null;
        }
    }

    private ExecutionCache GetOrCreateExecutionCache()
    {
        var executionCache = _executionCache;
        if (executionCache != null)
        {
            return executionCache;
        }

        lock (_executionCacheLock)
        {
            executionCache = _executionCache;
            if (executionCache != null)
            {
                return executionCache;
            }

            executionCache = BuildExecutionCache();
            _executionCache = executionCache;
            return executionCache;
        }
    }

    private ExecutionCache BuildExecutionCache()
    {
        var basicDescriptors = new List<PluginDescriptor>();
        var noticeHandlers = new List<PluginHook<INoticeHandler>>();
        var responseInterceptors = new List<PluginHook<IResponseInterceptor>>();
        var bindingFailureHandlers = new List<PluginHook<IBindingFailureHandler>>();
        var pluginExceptionHandlers = new List<PluginHook<IPluginExceptionHandler>>();

        foreach (var descriptor in _pluginCatalog.GetExecutionPlan())
        {
            var pluginInfo = descriptor.PluginInfo;
            if (pluginInfo.InitializationFailed)
            {
                continue;
            }

            if (pluginInfo.PluginType == PluginType.Basic)
            {
                basicDescriptors.Add(descriptor);
                continue;
            }

            if (pluginInfo.PluginType != PluginType.Service)
            {
                continue;
            }

            var serviceProvider = descriptor.LoaderContext.BuildServiceProvider();
            var pluginInstance = ResolvePluginInstance(serviceProvider, pluginInfo);
            if (pluginInstance == null)
            {
                continue;
            }

            AddServiceHooks(pluginInfo,
                pluginInstance,
                noticeHandlers,
                responseInterceptors,
                bindingFailureHandlers,
                pluginExceptionHandlers);
        }

        return new ExecutionCache(basicDescriptors.ToArray(),
            noticeHandlers.ToArray(),
            responseInterceptors.ToArray(),
            bindingFailureHandlers.ToArray(),
            pluginExceptionHandlers.ToArray());
    }

    private static PluginBase? ResolvePluginInstance(IServiceProvider serviceProvider, PluginInfo pluginInfo)
    {
        var instance = serviceProvider.GetService(pluginInfo.Type) as PluginBase;
        if (instance != null)
        {
            return instance;
        }

        return pluginInfo.ServiceType == null
            ? null
            : serviceProvider.GetService(pluginInfo.ServiceType) as PluginBase;
    }

    private static void AddServiceHooks(PluginInfo pluginInfo,
        PluginBase pluginInstance,
        ICollection<PluginHook<INoticeHandler>> noticeHandlers,
        ICollection<PluginHook<IResponseInterceptor>> responseInterceptors,
        ICollection<PluginHook<IBindingFailureHandler>> bindingFailureHandlers,
        ICollection<PluginHook<IPluginExceptionHandler>> pluginExceptionHandlers)
    {
        if (pluginInstance is INoticeHandler noticeHandler)
        {
            noticeHandlers.Add(new PluginHook<INoticeHandler>(pluginInfo, noticeHandler));
        }

        if (pluginInstance is IResponseInterceptor responseInterceptor)
        {
            responseInterceptors.Add(new PluginHook<IResponseInterceptor>(pluginInfo, responseInterceptor));
        }

        if (pluginInstance is IBindingFailureHandler bindingFailureHandler)
        {
            bindingFailureHandlers.Add(new PluginHook<IBindingFailureHandler>(pluginInfo, bindingFailureHandler));
        }

        if (pluginInstance is IPluginExceptionHandler pluginExceptionHandler)
        {
            pluginExceptionHandlers.Add(new PluginHook<IPluginExceptionHandler>(pluginInfo, pluginExceptionHandler));
        }
    }

    private sealed class PluginExecutionContext(
        IReadOnlyList<IServiceScope> scopes,
        IReadOnlyList<PluginExecutionInfo> basicPlugins,
        IReadOnlyList<PluginHook<INoticeHandler>> noticeHandlers,
        IReadOnlyList<PluginHook<IResponseInterceptor>> responseInterceptors,
        IReadOnlyList<PluginHook<IBindingFailureHandler>> bindingFailureHandlers,
        IReadOnlyList<PluginHook<IPluginExceptionHandler>> pluginExceptionHandlers)
        : IAsyncDisposable
    {
        public IReadOnlyList<IServiceScope> Scopes { get; } = scopes;
        public IReadOnlyList<PluginExecutionInfo> BasicPlugins { get; } = basicPlugins;
        public IReadOnlyList<PluginHook<INoticeHandler>> NoticeHandlers { get; } = noticeHandlers;
        public IReadOnlyList<PluginHook<IResponseInterceptor>> ResponseInterceptors { get; } = responseInterceptors;
        public IReadOnlyList<PluginHook<IBindingFailureHandler>> BindingFailureHandlers { get; } =
            bindingFailureHandlers;
        public IReadOnlyList<PluginHook<IPluginExceptionHandler>> PluginExceptionHandlers { get; } =
            pluginExceptionHandlers;

        public async ValueTask DisposeAsync()
        {
            foreach (var pluginExecution in BasicPlugins)
            {
                if (pluginExecution.NeedToDispose)
                {
                    await pluginExecution.PluginInstance.OnUninitialized();
                }
            }

            foreach (var scope in Scopes)
            {
                scope.Dispose();
            }
        }
    }

    private sealed class PluginExecutionInfo(
        PluginBase pluginInstance,
        PluginInfo pluginInfo,
        bool needToDispose,
        IServiceScope basedServiceScope)
    {
        public PluginBase PluginInstance { get; } = pluginInstance;
        public PluginInfo PluginInfo { get; } = pluginInfo;
        public bool NeedToDispose { get; } = needToDispose;
        public IServiceScope BasedServiceScope { get; } = basedServiceScope;
    }

    private sealed class PluginHook<THook>(PluginInfo pluginInfo, THook hook)
        where THook : class
    {
        public PluginInfo PluginInfo { get; } = pluginInfo;
        public THook Hook { get; } = hook;
    }

    private sealed class ExecutionCache(
        IReadOnlyList<PluginDescriptor> basicDescriptors,
        IReadOnlyList<PluginHook<INoticeHandler>> noticeHandlers,
        IReadOnlyList<PluginHook<IResponseInterceptor>> responseInterceptors,
        IReadOnlyList<PluginHook<IBindingFailureHandler>> bindingFailureHandlers,
        IReadOnlyList<PluginHook<IPluginExceptionHandler>> pluginExceptionHandlers)
    {
        public IReadOnlyList<PluginDescriptor> BasicDescriptors { get; } = basicDescriptors;
        public IReadOnlyList<PluginHook<INoticeHandler>> NoticeHandlers { get; } = noticeHandlers;
        public IReadOnlyList<PluginHook<IResponseInterceptor>> ResponseInterceptors { get; } = responseInterceptors;
        public IReadOnlyList<PluginHook<IBindingFailureHandler>> BindingFailureHandlers { get; } =
            bindingFailureHandlers;
        public IReadOnlyList<PluginHook<IPluginExceptionHandler>> PluginExceptionHandlers { get; } =
            pluginExceptionHandlers;
    }
}
