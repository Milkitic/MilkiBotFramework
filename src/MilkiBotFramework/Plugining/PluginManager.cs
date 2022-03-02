﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Event;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Plugining
{
    public class PluginManager
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
                await HandleMessage(e.MessageContext);
        }

        public string PluginBaseDirectory { get; internal set; }

        //todo: Same command; Same guid
        public async Task InitializeAllPlugins()
        {
            var sw = Stopwatch.StartNew();
            if (!Directory.Exists(PluginBaseDirectory)) Directory.CreateDirectory(PluginBaseDirectory);
            var directories = Directory.GetDirectories(PluginBaseDirectory);

            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory, "*.dll");
                var contextName = Path.GetFileName(directory);
                CreateContextAndAddPlugins(contextName, files);
            }

            var entryAsm = Assembly.GetEntryAssembly();
            if (entryAsm != null)
            {
                var dir = Path.GetDirectoryName(entryAsm.Location)!;
                var context = AssemblyLoadContext.Default.Assemblies;
                CreateContextAndAddPlugins(null, context
                    .Where(k => !k.IsDynamic && k.Location.StartsWith(dir))
                    .Select(k => k.Location)
                );
            }

            foreach (var loaderContext in _loaderContexts.Values)
            {
                var serviceProvider = loaderContext.BuildServiceProvider();

                foreach (var assemblyContext in loaderContext.AssemblyContexts.Values)
                {
                    foreach (var pluginInfo in assemblyContext.PluginInfos
                                 .Where(o => o.Lifetime == PluginLifetime.Singleton))
                    {
                        try
                        {
                            var instance = (PluginBase)serviceProvider.GetService(pluginInfo.Type);
                            InitializePlugin(instance, pluginInfo);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error while initializing plugin " + pluginInfo.Metadata.Name);
                        }
                    }
                }
            }

            _logger.LogInformation($"Plugin initialization done in {sw.Elapsed.TotalSeconds:N3}s!");
        }

        private void CreateContextAndAddPlugins(string? contextName, IEnumerable<string> files)
        {
            var assemblyResults = AssemblyHelper.AnalyzePluginsInAssemblyFilesByDnlib(_logger, files);
            if (assemblyResults.Count <= 0 || assemblyResults.All(k => k.TypeResults.Length == 0))
                return;

            var isRuntimeContext = contextName == null;

            var ctx = !isRuntimeContext
                ? new AssemblyLoadContext(contextName, true)
                : AssemblyLoadContext.Default;
            var loaderContext = new LoaderContext
            {
                AssemblyLoadContext = ctx,
                ServiceCollection = new ServiceCollection(),
                Name = contextName ?? "Runtime Context",
                IsRuntimeContext = isRuntimeContext
            };

            foreach (var assemblyResult in assemblyResults)
            {
                var assemblyPath = assemblyResult.AssemblyPath;
                var assemblyFullName = assemblyResult.AssemblyFullName;
                var assemblyFilename = Path.GetFileName(assemblyPath);
                var typeResults = assemblyResult.TypeResults;

                if (typeResults.Length == 0)
                {
                    if (isRuntimeContext) continue;

                    try
                    {
                        var inEntryAssembly =
                            AssemblyLoadContext.Default.Assemblies.FirstOrDefault(k =>
                                k.FullName == assemblyFullName);
                        if (inEntryAssembly != null)
                        {
                            ctx.LoadFromAssemblyName(inEntryAssembly.GetName());
                            _logger.LogDebug($"Dependency loaded {assemblyFilename} (Host)");
                        }
                        else
                        {
                            ctx.LoadFromAssemblyPath(assemblyPath);
                            _logger.LogDebug($"Dependency loaded {assemblyFilename} (Plugin)");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to load dependency {assemblyFilename}: {ex.Message}");
                    } // add dependencies

                    continue;
                }

                bool isValid = false;

                try
                {
                    Assembly? asm = isRuntimeContext
                        ? Assembly.GetEntryAssembly()
                        : ctx.LoadFromAssemblyPath(assemblyPath);
                    if (asm != null)
                    {
                        var asmContext = new AssemblyContext
                        {
                            Assembly = asm
                        };

                        foreach (var typeResult in typeResults)
                        {
                            var typeFullName = typeResult.TypeFullName!;
                            var baseType = typeResult.BaseType!;
                            string typeName = "";
                            PluginInfo? pluginInfo = null;
                            try
                            {
                                var type = asm.GetType(typeFullName);
                                if (type == null) throw new Exception("Can't resolve type: " + typeFullName);

                                typeName = type.Name;
                                pluginInfo = GetPluginInfo(type, baseType);
                                var metadata = pluginInfo.Metadata;

                                switch (pluginInfo.Lifetime)
                                {
                                    case PluginLifetime.Singleton:
                                        loaderContext.ServiceCollection.AddSingleton(type);
                                        break;
                                    case PluginLifetime.Scoped:
                                        loaderContext.ServiceCollection.AddScoped(type);
                                        break;
                                    case PluginLifetime.Transient:
                                        loaderContext.ServiceCollection.AddTransient(type);
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException();
                                }

                                _logger.LogInformation($"Add plugin \"{metadata.Name}\": " +
                                                       $"Author={string.Join(",", metadata.Authors)}; " +
                                                       $"Version={metadata.Version}; " +
                                                       $"Lifetime={pluginInfo.Lifetime} " +
                                                       $"({pluginInfo.BaseType.Name})");
                                isValid = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error occurs while loading plugin: " + typeName);
                            }

                            if (pluginInfo != null)
                            {
                                asmContext.PluginInfos.Add(pluginInfo);
                                _plugins.Add(pluginInfo);
                            }
                        }

                        if (isValid)
                        {
                            loaderContext.AssemblyContexts.Add(assemblyFilename, asmContext);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }

                if (!isValid)
                {
                    if (!isRuntimeContext)
                        _logger.LogWarning($"\"{assemblyFilename}\" 不是合法的插件扩展。");
                }
            }

            InitializeLoaderContext(loaderContext);
        }

        private void InitializeLoaderContext(LoaderContext loaderContext)
        {
            if (loaderContext.AssemblyLoadContext != AssemblyLoadContext.Default)
            {
                var existAssemblies = loaderContext.AssemblyLoadContext.Assemblies.Select(k => k.FullName).ToHashSet();

                foreach (var assembly in AssemblyLoadContext.Default.Assemblies)
                {
                    if (!assembly.IsDynamic && !existAssemblies.Contains(assembly.FullName))
                    {
                        loaderContext.AssemblyLoadContext.LoadFromAssemblyName(assembly.GetName());
                    }
                }
            }

            var allTypes = _serviceCollection
                .Where(o => o.Lifetime == ServiceLifetime.Singleton);
            foreach (var serviceDescriptor in allTypes)
            {
                var ns = serviceDescriptor.ServiceType.Namespace;
                if (serviceDescriptor.ImplementationType == serviceDescriptor.ServiceType)
                {
                    if (/*ns.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) || */
                        ns.StartsWith("Microsoft.Extensions.Options", StringComparison.Ordinal) ||
                        ns.StartsWith("Microsoft.Extensions.Logging", StringComparison.Ordinal))
                        continue;
                    var instance = _serviceProvider.GetService(serviceDescriptor.ImplementationType);
                    if (instance == null)
                        loaderContext.ServiceCollection.AddSingleton(serviceDescriptor.ImplementationType, _ => null!);
                    else
                        loaderContext.ServiceCollection.AddSingleton(serviceDescriptor.ImplementationType, instance);
                }
                else
                {
                    if (/*ns.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||*/
                        ns.StartsWith("Microsoft.Extensions.Options", StringComparison.Ordinal) ||
                        ns.StartsWith("Microsoft.Extensions.Logging", StringComparison.Ordinal))
                        continue;
                    var instance = _serviceProvider.GetService(serviceDescriptor.ServiceType);
                    if (instance == null)
                        loaderContext.ServiceCollection.AddSingleton(serviceDescriptor.ServiceType, _ => null!);
                    else
                        loaderContext.ServiceCollection.AddSingleton(serviceDescriptor.ServiceType, instance);
                }
            }

            var configLoggerProvider = _serviceProvider.GetService<ConfigLoggerProvider>();
            if (configLoggerProvider != null)
                loaderContext.ServiceCollection.AddLogging(o => configLoggerProvider.ConfigureLogger!(o));

            loaderContext.BuildServiceProvider();
            _loaderContexts.Add(loaderContext.Name, loaderContext);
        }

        private static void InitializePlugin(PluginBase instance, PluginInfo pluginInfo)
        {
            instance.Metadata = pluginInfo.Metadata;
            instance.IsInitialized = true;
            instance.OnInitialized();
        }

        private PluginInfo GetPluginInfo(Type type, Type baseType)
        {
            var lifetime = type.GetCustomAttribute<PluginLifetimeAttribute>()?.Lifetime ??
                           throw new ArgumentNullException(nameof(PluginLifetimeAttribute.Lifetime),
                               "The plugin lifetime is undefined: " + type.FullName);

            var identifierAttribute = type.GetCustomAttribute<PluginIdentifierAttribute>() ??
                                      throw new Exception("The plugin identifier is undefined: " + type.FullName);
            var guid = identifierAttribute.Guid;
            var index = identifierAttribute.Index;
            var name = identifierAttribute.Name ?? type.Name;
            var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "Nothing here.";
            var version = type.GetCustomAttribute<VersionAttribute>()?.Version ?? "0.0.1-alpha";
            var authors = type.GetCustomAttribute<AuthorAttribute>()?.Author ?? DefaultAuthors;

            var metadata = new PluginMetadata(Guid.Parse(guid), name, description, version, authors);

            var methodSets = new HashSet<string>();
            var commands = new Dictionary<string, CommandInfo>();
            foreach (var methodInfo in type.GetMethods())
            {
                if (methodSets.Contains(methodInfo.Name))
                    throw new ArgumentException(
                        "Duplicate method name with CommandHandler definition is not supported.", methodInfo.Name);

                methodSets.Add(methodInfo.Name);
                var commandHandlerAttribute = methodInfo.GetCustomAttribute<CommandHandlerAttribute>();
                if (commandHandlerAttribute == null) continue;

                var command = commandHandlerAttribute.Command ?? methodInfo.Name.ToLower();
                var methodDescription = methodInfo.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

                var parameterInfos = new List<CommandParameterInfo>();
                var parameters = methodInfo.GetParameters();
                foreach (var parameter in parameters)
                {
                    var targetType = parameter.ParameterType;
                    var attrs = parameter.GetCustomAttributes(false);
                    var parameterInfo = GetParameterInfo(attrs, targetType, parameter);
                    parameterInfos.Add(parameterInfo);
                }

                CommandReturnType returnType;
                var retType = methodInfo.ReturnType;
                if (retType == StaticTypes.Void)
                    returnType = CommandReturnType.Void;
                else if (retType == StaticTypes.Task)
                    returnType = CommandReturnType.Task;
                else if (retType == StaticTypes.ValueTask)
                    returnType = CommandReturnType.ValueTask;
                else if (retType == StaticTypes.IResponse)
                    returnType = CommandReturnType.IResponse;
                else
                {
                    if (retType.IsGenericType)
                    {
                        var genericDef = retType.GetGenericTypeDefinition();
                        if (genericDef == StaticTypes.Task_ &&
                            retType.GenericTypeArguments[0] == StaticTypes.IResponse)
                            returnType = CommandReturnType.Task_IResponse;
                        else if (genericDef == StaticTypes.ValueTask_ &&
                                 retType.GenericTypeArguments[0] == StaticTypes.IResponse)
                            returnType = CommandReturnType.ValueTask_IResponse;
                        else if (genericDef == StaticTypes.IEnumerable_ &&
                                 retType.GenericTypeArguments[0] == StaticTypes.IResponse)
                            returnType = CommandReturnType.IEnumerable_IResponse;
                        else if (genericDef == StaticTypes.IAsyncEnumerable_ &&
                                 retType.GenericTypeArguments[0] == StaticTypes.IResponse)
                            returnType = CommandReturnType.IAsyncEnumerable_IResponse;
                        else
                            returnType = CommandReturnType.Dynamic;
                    }
                    else
                        returnType = CommandReturnType.Dynamic;
                }

                var commandInfo = new CommandInfo(command, methodDescription, methodInfo, returnType,
                    parameterInfos.ToArray());

                commands.Add(command, commandInfo);
            }

            return new PluginInfo
            {
                Metadata = metadata,
                BaseType = baseType,
                Type = type,
                Lifetime = lifetime,
                Index = index,
                Commands = new ReadOnlyDictionary<string, CommandInfo>(commands)
            };
        }

        private CommandParameterInfo GetParameterInfo(object[] attrs, Type targetType,
            ParameterInfo parameter)
        {
            var parameterInfo = new CommandParameterInfo
            {
                ParameterName = parameter.Name!,
                ParameterType = targetType,
            };

            bool isReady = false;
            foreach (var attr in attrs)
            {
                if (attr is OptionAttribute option)
                {
                    parameterInfo.Abbr = option.Abbreviate;
                    parameterInfo.DefaultValue = parameter.DefaultValue == DBNull.Value
                        ? option.DefaultValue
                        : parameter.DefaultValue;
                    parameterInfo.Name = option.Name;
                    parameterInfo.ValueConverter = _commandLineAnalyzer.DefaultParameterConverter;
                    isReady = true;
                }
                else if (attr is ArgumentAttribute argument)
                {
                    parameterInfo.DefaultValue = parameter.DefaultValue == DBNull.Value
                        ? argument.DefaultValue
                        : parameter.DefaultValue;

                    parameterInfo.IsArgument = true;
                    parameterInfo.ValueConverter = _commandLineAnalyzer.DefaultParameterConverter;
                    isReady = true;
                }
                else if (attr is DescriptionAttribute description)
                {
                    parameterInfo.Description = description.Description;
                    //parameterInfo.HelpAuthority = help.Authority;
                }
            }

            if (!isReady)
            {
                parameterInfo.IsServiceArgument = true;
                parameterInfo.IsArgument = true;
            }

            return parameterInfo;
        }

        private async Task HandleMessage(MessageContext messageContext)
        {
            if (messageContext.MessageUserIdentity != null &&
                _asyncMessageDict.TryGetValue(messageContext.MessageUserIdentity, out var asyncMsg))
            {
                asyncMsg.SetMessage(new AsyncMessageResponse(messageContext.MessageId!,
                    messageContext.TextMessage!,
                    messageContext.ReceivedTime,
                    s => _richMessageConverter.Decode(s.AsMemory())));
                return;
            }

            List<(IMessagePlugin plugin, bool dispose, PluginInfo pluginInfo, IServiceScope serviceScope)> plugins =
                new();
            List<(ServicePlugin plugin, PluginInfo pluginInfo)> servicePlugins = new();
            var scopes = new HashSet<IServiceScope>();

            foreach (var loaderContext in _loaderContexts.Values)
            {
                var serviceScope = loaderContext.BuildServiceProvider().CreateScope();
                scopes.Add(serviceScope);
                foreach (var assemblyContext in loaderContext.AssemblyContexts.Values)
                {
                    foreach (var pluginInfo in assemblyContext.PluginInfos)
                    {
                        var pluginInstance = serviceScope.ServiceProvider.GetService(pluginInfo.Type)!;
                        if (pluginInfo.BaseType != StaticTypes.BasicPlugin &&
                            pluginInfo.BaseType != StaticTypes.BasicPlugin_)
                        {
                            if (pluginInfo.BaseType == StaticTypes.ServicePlugin)
                                servicePlugins.Add(((ServicePlugin)pluginInstance, pluginInfo));
                            continue;
                        }

                        var messagePlugin = (IMessagePlugin)pluginInstance;
                        if (pluginInfo.Lifetime != PluginLifetime.Singleton)
                        {
                            InitializePlugin((PluginBase)pluginInstance, pluginInfo);
                            plugins.Add((messagePlugin, true, pluginInfo, serviceScope));
                        }
                        else
                        {
                            plugins.Add((messagePlugin, false, pluginInfo, serviceScope));
                        }
                    }
                }
            }

            var nextPlugins = (List<PluginInfo>)messageContext.NextPlugins;
            var executedPlugins = (List<PluginInfo>)messageContext.ExecutedPlugins;

            var orderedArray = plugins.OrderBy(k => k.pluginInfo.Index).ToArray();
            nextPlugins.AddRange(orderedArray.Select(k => k.pluginInfo));

            var message = messageContext.TextMessage;
            var success = _commandLineAnalyzer.TryAnalyze(message, out var commandLineResult, out var exception);
            ReadOnlyMemory<char>? commandName = null;
            if (success)
            {
                commandName = commandLineResult?.Command;
                messageContext.CommandLineResult = commandLineResult!;
            }
            else if (exception != null)
                _logger.LogWarning("Error occurs while analyzing command: " + (exception?.Message ?? "Unknown reason"));

            bool handled = false;
            foreach (var (pluginInstance, dispose, pluginInfo, serviceScope) in orderedArray)
            {
                var plugin = (PluginBase)pluginInstance;
                if (!nextPlugins.Contains(pluginInfo))
                    continue;

                nextPlugins.Remove(pluginInfo);
                executedPlugins.Add(pluginInfo);

                try
                {
                    await plugin.OnExecuting();
                    if (commandName != null &&
                        pluginInfo.Commands.TryGetValue(commandName.Value.ToString(), out var commandInfo))
                    {
                        var asyncEnumerable = _commandLineInjector.InjectParametersAndRunAsync(commandLineResult!,
                            commandInfo, plugin, messageContext, serviceScope.ServiceProvider);
                        await foreach (var response in asyncEnumerable)
                        {
                            response?.Forced();
                            await SendAndCheckResponse(response);
                            if (handled) break;
                        }
                    }
                    else
                    {
                        var asyncEnumerable = pluginInstance.OnMessageReceived(messageContext);
                        await foreach (var response in asyncEnumerable)
                        {
                            await SendAndCheckResponse(response);
                            if (handled) break;
                        }
                    }

                    await plugin.OnExecuted();
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

            foreach (var (pluginInstance, dispose, pluginInfo, serviceScope) in orderedArray)
            {
                var plugin = (PluginBase)pluginInstance;
                if (dispose)
                    await plugin.OnUninitialized();
            }

            foreach (var serviceScope in scopes)
            {
                serviceScope.Dispose();
            }

            async Task SendAndCheckResponse(IResponse? response)
            {
                if (response == null) return;
                foreach (var (svcPlugin, _) in servicePlugins.OrderBy(k => k.pluginInfo.Index))
                {
                    await svcPlugin.BeforeSend(response);
                }

                handled = response.IsHandled;

                if (!handled && response.AsyncMessage is AsyncMessage asyncMessage)
                {
                    _asyncMessageDict.TryAdd(messageContext.MessageUserIdentity, asyncMessage);
                }

                if (response.Message == null) return;

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

                    var plainMessage = _richMessageConverter.Encode(response.Message);

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
                        _logger.LogWarning("Reply failed: destination undefined.");
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

                    var plainMessage = _richMessageConverter.Encode(response.Message);
                    if (response.MessageType == MessageType.Private)
                        await _messageApi.SendPrivateMessageAsync(response.Id!, plainMessage);
                    else if (response.MessageType == MessageType.Channel)
                        await _messageApi.SendChannelMessageAsync(response.Id!, message, response.SubId);
                    else
                        _logger.LogWarning("Send failed: destination undefined.");
                }

            }
        }
    }
}