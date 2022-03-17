using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining;

public partial class PluginManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceCollection _serviceCollection;
    private readonly BotOptions _botOptions;
    private readonly IDispatcher _dispatcher;
    private readonly IMessageApi _messageApi;
    private readonly IRichMessageConverter _richMessageConverter;
    private readonly ILogger<PluginManager> _logger;
    private readonly ICommandLineAnalyzer _commandLineAnalyzer;
    private readonly CommandInjector _commandInjector;

    // sub directory per loader
    private readonly HashSet<PluginInfo> _plugins = new();
    private readonly Dictionary<string, LoaderContext> _loaderContexts = new();
    private readonly EventBus _eventBus;

    private readonly ConcurrentDictionary<MessageUserIdentity, AsyncMessage> _asyncMessageDict = new();

    public PluginManager(IDispatcher dispatcher,
        IMessageApi messageApi,
        IRichMessageConverter richMessageConverter,
        ILogger<PluginManager> logger,
        ICommandLineAnalyzer commandLineAnalyzer,
        IServiceProvider serviceProvider,
        IServiceCollection serviceCollection,
        BotOptions botOptions,
        EventBus eventBus)
    {
        _serviceProvider = serviceProvider;
        _serviceCollection = serviceCollection;
        _botOptions = botOptions;
        _dispatcher = dispatcher;
        _messageApi = messageApi;
        _richMessageConverter = richMessageConverter;
        _logger = logger;
        _commandLineAnalyzer = commandLineAnalyzer;
        _commandInjector = new CommandInjector(commandLineAnalyzer,
            (ILogger<CommandInjector>)serviceProvider.GetService(typeof(Logger<CommandInjector>))!);
        _eventBus = eventBus;
        _eventBus.Subscribe<DispatchMessageEvent>(OnEventReceived);
    }

    public IReadOnlyList<PluginInfo> GetAllPlugins()
    {
        return _plugins.ToArray();
    }

    private async Task OnEventReceived(DispatchMessageEvent e)
    {
        if (e.MessageType is MessageType.Private or MessageType.Channel)
        {
            await HandleTextMessage(e.MessageContext);
        }
        else
        {
            await HandleNoticeMessage(e.MessageContext);
        }
    }

    private async Task HandleNoticeMessage(MessageContext messageContext)
    {
        var (scopes,
            basicExecutionInfos,
            serviceExecutionInfos) = await GetExecutionList(true);

        bool handled = false;
        foreach (var serviceExecutionInfo in serviceExecutionInfos)
        {
            var servicePlugin = (ServicePlugin)serviceExecutionInfo.PluginInstance;
            var response = await servicePlugin.OnNoticeReceived(messageContext);
            await SendAndCheckResponse(serviceExecutionInfo.PluginInfo, response);

            if (handled) break;
        }

        foreach (var serviceScope in scopes)
        {
            serviceScope.Dispose();
        }

        async Task SendAndCheckResponse(PluginInfo pluginInfo, IResponse? response)
        {
            if (response == null) return;
            if (response is MessageResponse mr)
                mr.MessageContext = messageContext;
            try
            {
                foreach (var serviceExecutionInfo in serviceExecutionInfos)
                {
                    var servicePlugin = (ServicePlugin)serviceExecutionInfo.PluginInstance;
                    var result = await servicePlugin.BeforeSend(pluginInfo, response);
                    if (!result)
                    {
                        response.IsHandled = true;
                        handled = response.IsHandled;
                        return;
                    }
                }

                handled = response.IsHandled;

                if (response.Message == null) return;
                await AutoReply(messageContext, response);
            }
            finally
            {
                if (response.Message is IDisposable d) d.Dispose();
                else if (response.Message is IAsyncDisposable ad) await ad.DisposeAsync();
            }
        }
    }

    private async Task HandleTextMessage(MessageContext messageContext)
    {
        if (messageContext.MessageUserIdentity != null &&
            _asyncMessageDict.TryGetValue(messageContext.MessageUserIdentity, out var asyncMsg))
        {
            asyncMsg.SetMessage(new AsyncMessageResponse(messageContext.MessageId!,
                messageContext.TextMessage!,
                messageContext.ReceivedTime,
                s => _richMessageConverter.Decode(s.AsMemory()),
                s =>
                {
                    _commandLineAnalyzer.TryAnalyze(s, out var result, out var ex);
                    if (ex != null) throw ex;
                    return result;
                }));
            return;
        }

        var (scopes,
            basicExecutionInfos,
            serviceExecutionInfos) = await GetExecutionList(false);

        var message = messageContext.TextMessage;
        var success = _commandLineAnalyzer.TryAnalyze(message, out var commandLineResult, out var exception);
        string? commandName = null;
        if (success)
        {
            commandName = commandLineResult?.Command.ToString();
            messageContext.CommandLineResult = commandLineResult!;
        }
        else if (exception != null)
            _logger.LogWarning("Error occurs while analyzing command: " + (exception?.Message ?? "Unknown reason"));

        messageContext.NextPlugins = new List<PluginInfo>(basicExecutionInfos.Count);
        var nextPlugins = messageContext.NextPlugins;
        var executedPlugins = (List<PluginInfo>)messageContext.ExecutedPlugins;
        nextPlugins.AddRange(basicExecutionInfos.Select(pluginExecutionInfo => pluginExecutionInfo.PluginInfo));

        bool handled = false;
        foreach (var pluginExecutionInfo in basicExecutionInfos)
        {
            var pluginInstance = pluginExecutionInfo.PluginInstance;
            var pluginInfo = pluginExecutionInfo.PluginInfo;
            var serviceProvider = pluginExecutionInfo.BasedServiceScope.ServiceProvider;

            if (!nextPlugins.Contains(pluginInfo))
                continue;

            nextPlugins.Remove(pluginInfo);
            executedPlugins.Add(pluginInfo);

            try
            {
                await pluginInstance.OnExecuting();
                if (commandName != null && pluginInfo.Commands.TryGetValue(commandName, out var commandInfo))
                {
                    try
                    {
                        var asyncEnumerable = _commandInjector.InjectParametersAndRunAsync(commandLineResult!,
                            commandInfo, pluginInstance, messageContext, serviceProvider);
                        await foreach (var response in asyncEnumerable)
                        {
                            if (response is { IsForced: null }) response.Forced();
                            await SendAndCheckResponse(pluginInfo, response);
                            if (handled) break;
                        }
                    }
                    catch (BindingException ex)
                    {
                        var errMsg = $"Command binding failed ({ex.BindingFailureType}; /{ex.BindingSource.CommandInfo.Command}";
                        if (ex.BindingSource.ParameterInfo != null)
                            errMsg += $".{(ex.BindingSource.ParameterInfo.Name ?? ex.BindingSource.ParameterInfo.ParameterName)}";
                        errMsg += "). Message: " + ex.Message;
                        _logger.LogWarning(errMsg);

                        var messagePlugin = (IMessagePlugin)pluginInstance;
                        var response = await messagePlugin.OnBindingFailed(ex, messageContext);
                        if (response == null!)
                        {
                            foreach (var serviceExecutionInfo in serviceExecutionInfos)
                            {
                                var servicePlugin = (ServicePlugin)serviceExecutionInfo.PluginInstance;
                                var result = await servicePlugin.OnBindingFailed(ex, messageContext);
                                await SendAndCheckResponse(pluginInfo, result);
                                if (handled) break;
                            }
                        }
                        else
                        {
                            await SendAndCheckResponse(pluginInfo, response);
                        }
                    }
                }
                else
                {
                    var messagePlugin = (IMessagePlugin)pluginInstance;
                    var asyncEnumerable = messagePlugin.OnMessageReceived(messageContext);
                    await foreach (var response in asyncEnumerable)
                    {
                        await SendAndCheckResponse(pluginInfo, response);
                        if (handled) break;
                    }
                }

                await pluginInstance.OnExecuted();
            }
            catch (Exception ex)
            {
                if (ex is AsyncMessageTimeoutException e)
                {
                    _logger.LogWarning(e.Message + ": " + pluginInfo.Metadata.Name);
                }
                else
                {
                    foreach (var serviceExecutionInfo in serviceExecutionInfos)
                    {
                        var servicePlugin = (ServicePlugin)serviceExecutionInfo.PluginInstance;
                        var result = await servicePlugin.OnPluginException(ex.InnerException ?? ex, messageContext);
                        await SendAndCheckResponse(pluginInfo, result);
                        if (handled) break;
                    }

                    _logger.LogError(ex, "Error Occurs while executing plugin: " + pluginInfo.Metadata.Name +
                                         ". User input: " + message);
                }
            }

            if (messageContext.MessageUserIdentity != null)
                _asyncMessageDict.TryRemove(messageContext.MessageUserIdentity, out _);

            if (handled) break;
        }

        foreach (var pluginExecutionInfo in basicExecutionInfos)
        {
            if (pluginExecutionInfo.NeedToDispose)
                await pluginExecutionInfo.PluginInstance.OnUninitialized();
        }

        foreach (var serviceScope in scopes)
        {
            serviceScope.Dispose();
        }

        async Task SendAndCheckResponse(PluginInfo pluginInfo, IResponse? response)
        {
            if (response == null) return;
            if (response is MessageResponse mr)
                mr.MessageContext = messageContext;
            try
            {
                foreach (var serviceExecutionInfo in serviceExecutionInfos)
                {
                    var servicePlugin = (ServicePlugin)serviceExecutionInfo.PluginInstance;
                    var result = await servicePlugin.BeforeSend(pluginInfo, response);
                    if (!result)
                    {
                        response.IsHandled = true;
                        handled = response.IsHandled;
                        return;
                    }
                }

                handled = response.IsHandled;

                if (!handled && response.AsyncMessage is AsyncMessage asyncMessage)
                {
                    _asyncMessageDict.AddOrUpdate(messageContext.MessageUserIdentity, asyncMessage, (_, _) => asyncMessage);
                }

                if (response.Message == null) return;
                await AutoReply(messageContext, response);
            }
            finally
            {
                if (response.Message is IDisposable d) d.Dispose();
                else if (response.Message is IAsyncDisposable ad) await ad.DisposeAsync();
            }
        }
    }

    private async Task AutoReply(MessageContext messageContext, IResponse response)
    {
        if (_botOptions.Variables.Count > 0)
        {
            switch (response.Message)
            {
                case Text t:
                    foreach (var (key, value) in _botOptions.Variables)
                    {
                        t.Content = t.Content.Replace($"${{{key}}}", value);
                    }

                    break;
                case RichMessage richMessage:
                    foreach (var message in richMessage)
                    {
                        if (message is not Text text) continue;
                        foreach (var (key, value) in _botOptions.Variables)
                        {
                            text.Content = text.Content.Replace($"${{{key}}}", value);
                        }
                    }

                    break;
            }
        }

        if (response.Id == null)
        {
            var identity = messageContext.MessageIdentity;
            if (identity?.MessageType == MessageType.Channel &&
                response.TryReply == true &&
                response.Message is not RichMessage { FirstIsReply: true } &&
                response.Message is not Reply)
            {
                response.Message =
                    new RichMessage(new Reply(messageContext.MessageId!), response.Message);
            }

            var plainMessage = await _richMessageConverter.EncodeAsync(response.Message);

            if (identity != null &&
                identity != MessageIdentity.MetaMessage &&
                identity != MessageIdentity.NoticeMessage)
            {
                if (identity.MessageType == MessageType.Private)
                    await _messageApi.SendPrivateMessageAsync(identity.Id!, plainMessage);
                else
                    await _messageApi.SendChannelMessageAsync(identity.Id!, plainMessage, identity.SubId);
            }
            else
            {
                _logger.LogWarning("Fail to reply: destination undefined.");
            }
        }
        else
        {
            if (response.MessageType == MessageType.Channel &&
                response.TryAt != null &&
                (response.Message is not RichMessage r || !r.FirstIsAt(response.TryAt!)) &&
                (response.Message is not At at || at.UserId != response.TryAt))
            {
                response.Message =
                    new RichMessage(new At(response.TryAt), response.Message);
            }

            var plainMessage = await _richMessageConverter.EncodeAsync(response.Message);
            if (response.MessageType == MessageType.Private)
                await _messageApi.SendPrivateMessageAsync(response.Id!, plainMessage);
            else if (response.MessageType == MessageType.Channel)
                await _messageApi.SendChannelMessageAsync(response.Id!, plainMessage, response.SubId);
            else
                _logger.LogWarning("Send failed: destination undefined.");
        }
    }

    private async Task<(HashSet<IServiceScope> scopes,
            List<PluginExecutionInfo> plugins,
            List<PluginExecutionInfo> servicePlugins)> GetExecutionList(
            bool isServiceOnly)
    {
        var scopes = new HashSet<IServiceScope>();
        var servicePlugins = new List<PluginExecutionInfo>();
        var plugins = new List<PluginExecutionInfo>();

        foreach (var loaderContext in _loaderContexts.Values)
        {
            var serviceScope = loaderContext.BuildServiceProvider().CreateScope();
            scopes.Add(serviceScope);
            foreach (var assemblyContext in loaderContext.AssemblyContexts.Values)
            {
                foreach (var pluginInfo in assemblyContext.PluginInfos)
                {
                    if (pluginInfo.InitializationFailed) continue;

                    var pluginInstance = (PluginBase)serviceScope.ServiceProvider.GetService(pluginInfo.Type)!;
                    if (pluginInfo.BaseType != StaticTypes.BasicPlugin &&
                        pluginInfo.BaseType != StaticTypes.BasicPlugin_)
                    {
                        if (pluginInfo.BaseType == StaticTypes.ServicePlugin)
                            servicePlugins.Add(new PluginExecutionInfo(pluginInstance, pluginInfo, false, serviceScope));
                        continue;
                    }

                    if (isServiceOnly) continue;
                    if (pluginInfo.Lifetime != PluginLifetime.Singleton)
                    {
                        try
                        {
                            await InitializePlugin(pluginInstance, pluginInfo);
                            plugins.Add(new PluginExecutionInfo(pluginInstance, pluginInfo, true, serviceScope));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error occurs while initializing plugin: " + pluginInfo.Metadata.Name);
                        }
                    }
                    else
                    {
                        plugins.Add(new PluginExecutionInfo(pluginInstance, pluginInfo, false, serviceScope));
                    }
                }
            }
        }

        plugins.Sort();
        servicePlugins.Sort();
        return (scopes, plugins, servicePlugins);
    }

    private class PluginExecutionInfo : IComparable<PluginExecutionInfo>
    {
        public readonly PluginBase PluginInstance;
        public readonly PluginInfo PluginInfo;
        public readonly bool NeedToDispose;
        public readonly IServiceScope BasedServiceScope;

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

        public int CompareTo(PluginExecutionInfo? other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            return PluginInfo.Index.CompareTo(other.PluginInfo.Index);
        }
    }
}