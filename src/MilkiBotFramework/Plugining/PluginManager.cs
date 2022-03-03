using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    private static readonly string[] DefaultAuthors = { "anonym" };

    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceCollection _serviceCollection;
    private readonly IDispatcher _dispatcher;
    private readonly IMessageApi _messageApi;
    private readonly IRichMessageConverter _richMessageConverter;
    private readonly ILogger<PluginManager> _logger;
    private readonly ICommandLineAnalyzer _commandLineAnalyzer;
    private readonly CommandLineInjector _commandLineInjector;

    // sub directory per loader
    private readonly Dictionary<string, LoaderContext> _loaderContexts = new();
    private readonly HashSet<PluginInfo> _plugins = new();
    private readonly EventBus _eventBus;

    private readonly ConcurrentDictionary<MessageUserIdentity, AsyncMessage> _asyncMessageDict = new();

    public PluginManager(IDispatcher dispatcher,
        IMessageApi messageApi,
        IRichMessageConverter richMessageConverter,
        ILogger<PluginManager> logger,
        ICommandLineAnalyzer commandLineAnalyzer,
        IServiceProvider serviceProvider,
        IServiceCollection serviceCollection,
        EventBus eventBus)
    {
        _serviceProvider = serviceProvider;
        _serviceCollection = serviceCollection;
        _dispatcher = dispatcher;
        _messageApi = messageApi;
        _richMessageConverter = richMessageConverter;
        _logger = logger;
        _commandLineAnalyzer = commandLineAnalyzer;
        _commandLineInjector = new CommandLineInjector(commandLineAnalyzer);
        _eventBus = eventBus;
        _eventBus.Subscribe<DispatchMessageEvent>(OnEventReceived);
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
        var scopes = GetExecutionList(
            out _,
            out var serviceExecutionInfos,
            true);

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
            try
            {
                foreach (var serviceExecutionInfo in serviceExecutionInfos)
                {
                    var servicePlugin = (ServicePlugin)serviceExecutionInfo.PluginInstance;
                    var result = await servicePlugin.BeforeSend(pluginInfo, response);
                    if (!result)
                    {
                        response.IsHandled = true;
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

        var scopes = GetExecutionList(
            out var basicExecutionInfos,
            out var serviceExecutionInfos,
            false);

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
                    var asyncEnumerable = _commandLineInjector.InjectParametersAndRunAsync(commandLineResult!,
                        commandInfo, pluginInstance, messageContext, serviceProvider);
                    await foreach (var response in asyncEnumerable)
                    {
                        response?.Forced();
                        await SendAndCheckResponse(pluginInfo, response);
                        if (handled) break;
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
            try
            {
                foreach (var serviceExecutionInfo in serviceExecutionInfos)
                {
                    var servicePlugin = (ServicePlugin)serviceExecutionInfo.PluginInstance;
                    var result = await servicePlugin.BeforeSend(pluginInfo, response);
                    if (!result)
                    {
                        response.IsHandled = true;
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
        if (response.Id == null)
        {
            var identity = messageContext.MessageIdentity;
            if (identity?.MessageType == MessageType.Channel &&
                response.TryReply &&
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

    private HashSet<IServiceScope> GetExecutionList(
        out List<PluginExecutionInfo> plugins,
        out List<PluginExecutionInfo> servicePlugins,
        bool isServiceOnly)
    {
        var scopes = new HashSet<IServiceScope>();
        servicePlugins = new List<PluginExecutionInfo>();
        plugins = new List<PluginExecutionInfo>();

        foreach (var loaderContext in _loaderContexts.Values)
        {
            var serviceScope = loaderContext.BuildServiceProvider().CreateScope();
            scopes.Add(serviceScope);
            foreach (var assemblyContext in loaderContext.AssemblyContexts.Values)
            {
                foreach (var pluginInfo in assemblyContext.PluginInfos)
                {
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
                            InitializePlugin(pluginInstance, pluginInfo);
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
        return scopes;
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