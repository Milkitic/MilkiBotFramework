﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContractsManaging.Models;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Utils;
using MemberInfo = MilkiBotFramework.ContractsManaging.Models.MemberInfo;

namespace MilkiBotFramework.Plugining.Loading
{
    public class PluginManager
    {
        private static readonly string[] DefaultAuthors = { "anonym" };

        private readonly IDispatcher _dispatcher;
        private readonly ILogger<PluginManager> _logger;
        private readonly ICommandLineAnalyzer _commandLineAnalyzer;

        // sub directory per loader
        private readonly Dictionary<string, LoaderContext> _loaderContexts = new();
        private readonly HashSet<PluginDefinition> _plugins = new();

        public PluginManager(IDispatcher dispatcher, ILogger<PluginManager> logger, ICommandLineAnalyzer commandLineAnalyzer)
        {
            _dispatcher = dispatcher;
            _logger = logger;
            _commandLineAnalyzer = commandLineAnalyzer;
            dispatcher.PrivateMessageReceived += Dispatcher_PrivateMessageReceived;
            dispatcher.ChannelMessageReceived += Dispatcher_ChannelMessageReceived;
        }

        public string PluginBaseDirectory { get; internal set; }
        public ServiceCollection BaseServiceCollection { get; internal set; }
        public IServiceProvider BaseServiceProvider { get; internal set; }

        private async Task Dispatcher_ChannelMessageReceived(MessageContext context, ChannelInfo channelInfo, MemberInfo memberInfo)
        {
        }

        private async Task Dispatcher_PrivateMessageReceived(MessageContext context, PrivateInfo privateInfo)
        {
            var message = context.Request.TextMessage;
            var success = _commandLineAnalyzer.TryAnalyze(message, out var commandLineResult, out var exception);
            if (!success && exception != null) throw exception;
            ReadOnlyMemory<char>? commandName = null;
            if (success) commandName = commandLineResult.Command;

            foreach (var loaderContext in _loaderContexts.Values)
            {
                using var serviceProvider = loaderContext.BuildServiceProvider().CreateScope();
                Dictionary<IMessagePlugin, (bool, PluginDefinition)> plugins = new();
                foreach (var assemblyContext in loaderContext.AssemblyContexts.Values)
                {
                    foreach (var pluginDefinition in assemblyContext.PluginDefinitions)
                    {
                        if (pluginDefinition.BaseType != typeof(BasicPlugin) &&
                            pluginDefinition.BaseType != typeof(BasicPlugin<>)) continue;

                        var pluginInstance = (IMessagePlugin)serviceProvider.ServiceProvider.GetService(pluginDefinition.Type)!;
                        if (pluginDefinition.Lifetime != PluginLifetime.Singleton)
                        {
                            InitializePlugin((PluginBase)pluginInstance, pluginDefinition);
                            plugins.Add(pluginInstance, (true, pluginDefinition));
                        }
                        else
                        {
                            plugins.Add(pluginInstance, (false, pluginDefinition));
                        }
                    }
                }

                foreach (var (pluginInstance, (dispose, pluginDefinition)) in plugins)
                {
                    var plugin = (PluginBase)pluginInstance;
                    await plugin.OnExecuting();
                    if (commandName != null &&
                        pluginDefinition.Commands.TryGetValue(commandName?.ToString(), out var commandDefinition))
                    {
                    }
                    else
                    {
                        await pluginInstance.OnMessageReceived(context);
                    }

                    await plugin.OnExecuted();
                }

                foreach (var (pluginInstance, (dispose, pluginDefinition)) in plugins)
                {
                    var plugin = (PluginBase)pluginInstance;
                    if (dispose) await plugin.OnUninitialized();
                }
            }
        }

        //todo: Same command; Same guid
        public async Task InitializeAllPlugins()
        {
            if (!Directory.Exists(PluginBaseDirectory)) Directory.CreateDirectory(PluginBaseDirectory);
            var directories = Directory.GetDirectories(PluginBaseDirectory);

            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory, "*.dll");
                var contextName = Path.GetFileName(directory);
                CreateContextAndAddPlugins(contextName, files);
            }

            CreateContextAndAddPlugins(null, Assembly.GetEntryAssembly().Location);

            foreach (var loaderContext in _loaderContexts.Values)
            {
                var serviceProvider = loaderContext.BuildServiceProvider();

                foreach (var assemblyContext in loaderContext.AssemblyContexts.Values)
                {
                    foreach (var pluginDefinition in assemblyContext.PluginDefinitions
                                 .Where(o => o.Lifetime == PluginLifetime.Singleton))
                    {
                        try
                        {
                            var instance = (PluginBase)serviceProvider.GetService(pluginDefinition.Type);
                            InitializePlugin(instance, pluginDefinition);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error while initializing plugin " + pluginDefinition.Metadata.Name);
                        }
                    }
                }
            }
        }

        private void CreateContextAndAddPlugins(string? contextName, params string[] files)
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

                if (typeResults.Length == 0 && !isRuntimeContext)
                {
                    try
                    {
                        var inEntryAssembly =
                            AssemblyLoadContext.Default.Assemblies.FirstOrDefault(k => k.FullName == assemblyFullName);
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
                            PluginDefinition? definition = null;
                            try
                            {
                                var type = asm.GetType(typeFullName);
                                if (type == null) throw new Exception("Can't resolve type: " + typeFullName);

                                typeName = type.Name;
                                definition = GetPluginDefinition(type, baseType);
                                var metadata = definition.Metadata;

                                switch (definition.Lifetime)
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
                                                       $"Lifetime={definition.Lifetime} " +
                                                       $"({definition.BaseType.Name})");
                                isValid = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error occurs while loading plugin: " + typeName);
                            }

                            if (definition != null)
                            {
                                asmContext.PluginDefinitions.Add(definition);
                                _plugins.Add(definition);
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
                    if (!existAssemblies.Contains(assembly.FullName))
                    {
                        loaderContext.AssemblyLoadContext.LoadFromAssemblyName(assembly.GetName());
                    }
                }
            }

            var allTypes = BaseServiceCollection
                .Where(o => o.Lifetime == ServiceLifetime.Singleton);
            foreach (var serviceDescriptor in allTypes)
            {
                var instance = serviceDescriptor.ImplementationType != null
                    ? BaseServiceProvider.GetService(serviceDescriptor.ImplementationType)
                    : BaseServiceProvider.GetService(serviceDescriptor.ServiceType);

                if (instance != null)
                    loaderContext.ServiceCollection.AddSingleton(serviceDescriptor.ServiceType, instance);
            }

            var bot = BaseServiceProvider.GetService<Bot>();
            if (bot != null) loaderContext.ServiceCollection.AddLogging(o => bot.Builder._configureLogger!(o));

            loaderContext.BuildServiceProvider();
            _loaderContexts.Add(loaderContext.Name, loaderContext);
        }

        private static void InitializePlugin(PluginBase instance, PluginDefinition pluginDefinition)
        {
            instance.Metadata = pluginDefinition.Metadata;
            instance.IsInitialized = true;
            instance.OnInitialized();
        }

        private static PluginDefinition GetPluginDefinition(Type type, Type baseType)
        {
            var lifetime = type.GetCustomAttribute<PluginLifetimeAttribute>()?.Lifetime ??
                           throw new ArgumentNullException(nameof(PluginLifetimeAttribute.Lifetime),
                               "The plugin lifetime is undefined: " + type.FullName);

            var identifierAttribute = type.GetCustomAttribute<PluginIdentifierAttribute>() ??
                                      throw new Exception("The plugin identifier is undefined: " + type.FullName);
            var guid = identifierAttribute.Guid;
            var name = identifierAttribute.Name ?? type.Name;
            var description = type.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "Nothing here.";
            var version = type.GetCustomAttribute<VersionAttribute>()?.Version ?? "0.0.1-alpha";
            var authors = type.GetCustomAttribute<AuthorAttribute>()?.Author ?? DefaultAuthors;

            var metadata = new PluginMetadata(Guid.Parse(guid), name, description, version, authors);

            var methodSets = new HashSet<string>();
            var commands = new Dictionary<string, PluginCommandDefinition>();
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

                var parameterDefinitions = new List<ParameterDefinition>();
                var parameters = methodInfo.GetParameters();
                foreach (var parameter in parameters)
                {
                    var targetType = parameter.ParameterType;
                    var attrs = parameter.GetCustomAttributes(false);
                    var parameterDefinition = GetParameterDefinition(attrs, targetType, parameter.Name);
                    parameterDefinitions.Add(parameterDefinition);
                }

                commands.Add(command, new PluginCommandDefinition(command, methodDescription, methodInfo.Name, parameterDefinitions));
            }

            return new PluginDefinition
            {
                Metadata = metadata,
                BaseType = baseType,
                Type = type,
                Lifetime = lifetime,
                Commands = commands
            };
        }

        private static ParameterDefinition GetParameterDefinition(object[] attrs, Type targetType, string parameterName)
        {
            var parameterDefinition = new ParameterDefinition { ParameterName = parameterName };

            bool isReady = false;
            foreach (var attr in attrs)
            {
                if (attr is OptionAttribute option)
                {
                    parameterDefinition.Abbr = option.Abbreviate;
                    parameterDefinition.DefaultValue = option.DefaultValue;
                    parameterDefinition.Name = option.Name;
                    parameterDefinition.ParameterType = targetType;
                    parameterDefinition.ValueConverter = DefaultConverter.Instance;
                    isReady = true;
                }
                else if (attr is ArgumentAttribute argument)
                {
                    parameterDefinition.DefaultValue = argument.DefaultValue;
                    parameterDefinition.ParameterType = targetType;
                    parameterDefinition.IsArgument = true;
                    parameterDefinition.ValueConverter = DefaultConverter.Instance;
                    isReady = true;
                }
                else if (attr is DescriptionAttribute description)
                {
                    parameterDefinition.Description = description.Description;
                    //parameterDefinition.HelpAuthority = help.Authority;
                }
            }

            if (!isReady)
            {
                parameterDefinition.IsServiceArgument = true;
                parameterDefinition.IsArgument = true;
            }

            return parameterDefinition;
        }
    }

}