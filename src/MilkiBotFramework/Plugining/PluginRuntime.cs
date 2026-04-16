using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.CommandLine;
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

    public PluginRuntime(ICommandLineAnalyzer commandLineAnalyzer,
        ILogger<PluginRuntime> logger,
        AsyncMessageSessionManager asyncMessageSessionManager,
        CommandInjector commandInjector,
        PluginCatalog pluginCatalog,
        PluginResponseDispatcher responseDispatcher)
    {
        _commandLineAnalyzer = commandLineAnalyzer;
        _logger = logger;
        _asyncMessageSessionManager = asyncMessageSessionManager;
        _commandInjector = commandInjector;
        _pluginCatalog = pluginCatalog;
        _responseDispatcher = responseDispatcher;
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
        await using var executionContext = await CreateExecutionContextAsync(includeBasicPlugins: false);
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

        await using var executionContext = await CreateExecutionContextAsync(includeBasicPlugins: true);

        var message = messageContext.TextMessage;
        string? commandName = null;
        CommandLineResult? commandLineResult = null;
        if (message != null)
        {
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

        var remainingPlugins = executionContext.BasicPlugins
            .Select(pluginExecution => pluginExecution.PluginInfo)
            .ToHashSet();

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
                        var errMsg = $"Command binding failed ({ex.BindingFailureType}; /{ex.BindingSource.CommandInfo.Command}";
                        if (ex.BindingSource.ParameterInfo != null)
                            errMsg += $".{(ex.BindingSource.ParameterInfo.Name ?? ex.BindingSource.ParameterInfo.ParameterName)}";
                        errMsg += "). Message: " + ex.Message;
                        _logger.LogWarning(errMsg);

                        var response = await ((IMessagePlugin)pluginInstance).OnBindingFailed(ex, messageContext);
                        if (response == null)
                        {
                            foreach (var bindingFailureHandler in executionContext.BindingFailureHandlers)
                            {
                                var fallbackResponse = await bindingFailureHandler.OnBindingFailed(ex, messageContext);
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
                        var response = await pluginExceptionHandler.OnPluginException(ex.InnerException ?? ex, messageContext);
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
                var shouldContinue = await responseInterceptor.BeforeSend(pluginInfo, response);
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
                    await responseInterceptor.AfterSend(pluginInfo, response);
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

    private async Task<PluginExecutionContext> CreateExecutionContextAsync(bool includeBasicPlugins)
    {
        var scopesByLoader = new Dictionary<LoaderContext, IServiceScope>();
        var scopes = new List<IServiceScope>();
        var basicPlugins = new List<PluginExecutionInfo>();
        var noticeHandlers = new List<PluginHook<INoticeHandler>>();
        var responseInterceptors = new List<IResponseInterceptor>();
        var bindingFailureHandlers = new List<IBindingFailureHandler>();
        var pluginExceptionHandlers = new List<IPluginExceptionHandler>();

        foreach (var descriptor in _pluginCatalog.GetExecutionPlan())
        {
            var pluginInfo = descriptor.PluginInfo;
            if (pluginInfo.InitializationFailed)
            {
                continue;
            }

            if (!scopesByLoader.TryGetValue(descriptor.LoaderContext, out var scope))
            {
                scope = descriptor.LoaderContext.BuildServiceProvider().CreateScope();
                scopesByLoader.Add(descriptor.LoaderContext, scope);
                scopes.Add(scope);
            }

            var pluginInstance = (PluginBase)scope.ServiceProvider.GetService(pluginInfo.Type)!;
            if (pluginInfo.PluginType == PluginType.Basic)
            {
                if (!includeBasicPlugins)
                {
                    continue;
                }

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

                continue;
            }

            if (pluginInfo.PluginType != PluginType.Service)
            {
                continue;
            }

            if (pluginInstance is INoticeHandler noticeHandler)
            {
                noticeHandlers.Add(new PluginHook<INoticeHandler>(pluginInfo, noticeHandler));
            }

            if (pluginInstance is IResponseInterceptor responseInterceptor)
            {
                responseInterceptors.Add(responseInterceptor);
            }

            if (pluginInstance is IBindingFailureHandler bindingFailureHandler)
            {
                bindingFailureHandlers.Add(bindingFailureHandler);
            }

            if (pluginInstance is IPluginExceptionHandler pluginExceptionHandler)
            {
                pluginExceptionHandlers.Add(pluginExceptionHandler);
            }
        }

        return new PluginExecutionContext(scopes,
            basicPlugins,
            noticeHandlers,
            responseInterceptors,
            bindingFailureHandlers,
            pluginExceptionHandlers);
    }

    private sealed class PluginExecutionContext : IAsyncDisposable
    {
        public PluginExecutionContext(List<IServiceScope> scopes,
            List<PluginExecutionInfo> basicPlugins,
            List<PluginHook<INoticeHandler>> noticeHandlers,
            List<IResponseInterceptor> responseInterceptors,
            List<IBindingFailureHandler> bindingFailureHandlers,
            List<IPluginExceptionHandler> pluginExceptionHandlers)
        {
            Scopes = scopes;
            BasicPlugins = basicPlugins;
            NoticeHandlers = noticeHandlers;
            ResponseInterceptors = responseInterceptors;
            BindingFailureHandlers = bindingFailureHandlers;
            PluginExceptionHandlers = pluginExceptionHandlers;
        }

        public List<IServiceScope> Scopes { get; }
        public List<PluginExecutionInfo> BasicPlugins { get; }
        public List<PluginHook<INoticeHandler>> NoticeHandlers { get; }
        public List<IResponseInterceptor> ResponseInterceptors { get; }
        public List<IBindingFailureHandler> BindingFailureHandlers { get; }
        public List<IPluginExceptionHandler> PluginExceptionHandlers { get; }

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

    private sealed class PluginExecutionInfo
    {
        public PluginExecutionInfo(PluginBase pluginInstance,
            PluginInfo pluginInfo,
            bool needToDispose,
            IServiceScope basedServiceScope)
        {
            PluginInstance = pluginInstance;
            PluginInfo = pluginInfo;
            NeedToDispose = needToDispose;
            BasedServiceScope = basedServiceScope;
        }

        public PluginBase PluginInstance { get; }
        public PluginInfo PluginInfo { get; }
        public bool NeedToDispose { get; }
        public IServiceScope BasedServiceScope { get; }
    }

    private sealed class PluginHook<THook>
        where THook : class
    {
        public PluginHook(PluginInfo pluginInfo, THook hook)
        {
            PluginInfo = pluginInfo;
            Hook = hook;
        }

        public PluginInfo PluginInfo { get; }
        public THook Hook { get; }
    }
}