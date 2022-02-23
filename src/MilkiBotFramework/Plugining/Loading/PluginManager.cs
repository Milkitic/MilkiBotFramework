using System;
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
using MilkiBotFramework.Utils;
using MemberInfo = MilkiBotFramework.ContractsManaging.Models.MemberInfo;

namespace MilkiBotFramework.Plugining.Loading
{
    public class PluginManager
    {
        private static readonly string[] DefaultAuthors = { "anonym" };

        private readonly IDispatcher _dispatcher;
        private readonly ILogger<PluginManager> _logger;

        // sub directory per loader
        private Dictionary<string, LoaderContext> _loaderContexts = new();

        public PluginManager(IDispatcher dispatcher, ILogger<PluginManager> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;
            dispatcher.PrivateMessageReceived += Dispatcher_PrivateMessageReceived;
            dispatcher.ChannelMessageReceived += Dispatcher_ChannelMessageReceived;
        }

        public string PluginBaseDirectory { get; internal set; }
        public ServiceCollection BaseServiceCollection { get; internal set; }
        public IServiceProvider BaseServiceProvider { get; internal set; }

        private async Task Dispatcher_ChannelMessageReceived(MessageRequestContext context, ChannelInfo channelInfo, MemberInfo memberInfo)
        {
        }

        private async Task Dispatcher_PrivateMessageReceived(MessageRequestContext requestContext, PrivateInfo privateInfo)
        {
        }

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
                var serviceProvider = loaderContext.ServiceProvider;

                foreach (var assemblyContext in loaderContext.AssemblyContexts.Values)
                {
                    foreach (var pluginDefinition in assemblyContext.PluginDefinitions.Where(o => o.Lifetime == PluginLifetime.Singleton))
                    {
                        var instance = (PluginBase)serviceProvider.GetService(pluginDefinition.Type);
                        InitializePlugin(instance, pluginDefinition);
                    }
                }
            }
        }

        private void CreateContextAndAddPlugins(string? contextName, params string[] files)
        {
            var assemblyResults = AssemblyHelper.AnalyzePluginsInAssemblyFilesByDnlib(_logger, files);
            if (assemblyResults.Count <= 0 || assemblyResults.All(k => k.TypeResults.Length == 0)) return;

            var isRuntimeContext = contextName == null;

            var ctx = !isRuntimeContext
                ? new AssemblyLoadContext(contextName, true)
                : AssemblyLoadContext.Default;
            var loaderContext = new LoaderContext()
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
                            var typeFullName = typeResult.TypeFullName;
                            var baseType = typeResult.BaseType;
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

                                _logger.LogInformation($"Added plugin \"{metadata.Name}\": " +
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

            var serviceProvider = loaderContext.BuildServiceProvider();
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

            return new PluginDefinition
            {
                Metadata = metadata,
                BaseType = baseType,
                Type = type,
                Lifetime = lifetime
            };
        }
    }

}
