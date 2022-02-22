using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContractsManaging.Models;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Messaging;
using MemberInfo = MilkiBotFramework.ContractsManaging.Models.MemberInfo;

namespace MilkiBotFramework.Plugins
{
    public class PluginManager
    {
        private static readonly Dictionary<string, Type> PluginTypes = new()
        {
            ["BasicPlugin`1"] = typeof(BasicPlugin<>),
            [nameof(BasicPlugin)] = typeof(BasicPlugin),
            [nameof(ServicePlugin)] = typeof(ServicePlugin)
        };

        private static readonly string[] DefaultAuthors = { "anonym" };

        private readonly IDispatcher _dispatcher;
        private readonly ILogger<PluginManager> _logger;

        // sub directory per loader
        private Dictionary<string, AssemblyContext> _assemblyLoaders = new();

        public PluginManager(IDispatcher dispatcher, ILogger<PluginManager> logger)
        {
            _dispatcher = dispatcher;
            _logger = logger;
            dispatcher.PrivateMessageReceived += Dispatcher_PrivateMessageReceived;
            dispatcher.ChannelMessageReceived += Dispatcher_ChannelMessageReceived;
        }

        public string PluginBaseDirectory { get; internal set; }

        private async Task Dispatcher_ChannelMessageReceived(MessageRequestContext context, ChannelInfo channelInfo, MemberInfo memberInfo)
        {
        }

        private async Task Dispatcher_PrivateMessageReceived(MessageRequestContext requestContext, PrivateInfo privateInfo)
        {
        }

        internal async Task InitializeAllPlugins()
        {
            if (!Directory.Exists(PluginBaseDirectory)) Directory.CreateDirectory(PluginBaseDirectory);
            var directories = Directory.GetDirectories(PluginBaseDirectory);

            foreach (var directory in directories)
            {
                var files = Directory.GetFiles(directory, "*.dll");
                AddByAssemblyFiles(Path.GetFileName(directory), files);
            }

            AddByAssemblyFiles(null, Assembly.GetEntryAssembly().Location);
        }

        private void AddByAssemblyFiles(string? contextName, params string[] files)
        {
            var availableDictionary = AnalyzeAssemblyFiles(files);

            if (availableDictionary.Count <= 0) return;
            var ctx = contextName == null
                ? new AssemblyLoadContext(contextName, true)
                : AssemblyLoadContext.Default;

            foreach (var k in availableDictionary)
            {
                var (asmPath, typeDefs) = k;
                var asmName = Path.GetFileName(asmPath);

                var asmContext = new AssemblyContext
                {
                    Assemblies = new Dictionary<string, Assembly>(),
                    AssemblyLoadContext = ctx,
                    ServiceCollection = new ServiceCollection()
                };

                if (k.Value.Length == 0 && contextName != null)
                {
                    try
                    {
                        ctx.LoadFromAssemblyPath(asmPath);
                        _logger.LogInformation($"Dependency loaded {asmName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to load dependency {asmName}: {ex.Message}");
                    } // add dependencies

                    continue;
                }

                bool isValid = false;
                try
                {
                    Assembly asm = contextName == null ? Assembly.GetEntryAssembly() : ctx.LoadFromAssemblyPath(asmPath);
                    foreach (var (typeDef, pluginType) in typeDefs)
                    {
                        string typeName = "";
                        try
                        {
                            var type = asm.GetType(typeDef.FullName);
                            typeName = type.Name;
                            InsertPlugin(type, pluginType, asmContext);
                            isValid = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, typeName + " 抛出了未处理的异常。");
                        }
                    }

                    if (isValid)
                    {
                        asmContext.Assemblies.Add(asmName, asm);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.Message);
                }

                if (!isValid)
                    _logger.LogWarning($"\"{asmName}\" 不是合法的插件扩展。");
            }
        }

        private Dictionary<string, (TypeDef, Type)[]> AnalyzeAssemblyFiles(string[] files)
        {
            var availableDictionary = new Dictionary<string, (TypeDef, Type)[]>();

            foreach (var asmPath in files)
            {
                var asmName = Path.GetFileName(asmPath);
                var modCtx = ModuleDef.CreateModuleContext();
                try
                {
                    _logger.LogInformation("Find " + asmName);
                    var module = ModuleDefMD.Load(asmPath, modCtx);
                    var types = module.HasTypes
                        ? module.Types.Select(k =>
                        {
                            ITypeDefOrRef? baseType = k;

                            Type? pluginType = null;
                            while (baseType != null && !PluginTypes.TryGetValue(baseType.Name, out pluginType))
                            {
                                var typeDef = baseType as TypeDef;
                                if (typeDef == null)
                                {
                                    baseType = null;
                                    break;
                                }

                                baseType = typeDef.BaseType;

                                if (baseType is { NumberOfGenericParameters: > 0 })
                                {
                                    if (PluginTypes.TryGetValue(baseType.ReflectionName, out pluginType))
                                    {
                                        baseType = baseType.ScopeType;
                                        break;
                                    }
                                }
                            }

                            var valid = baseType != null &&
                                        !k.IsAbstract &&
                                        k.IsPublic &&
                                        PluginTypes.ContainsKey(baseType.Name);
                            var result = valid ? k : null;
                            return (result, type: pluginType);
                        }).Where(k => k.result != null).ToArray()
                        : Array.Empty<(TypeDef, Type)>();
                    if (types.Length == 0)
                        _logger.LogWarning($"\"{asmName}\" has no valid classes.");
                    //else
                    availableDictionary.Add(module.Location, types);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to read assembly {asmName}: {ex.Message}");
                }
            }

            return availableDictionary;
        }


        private void InsertPlugin(Type type, Type pluginType, AssemblyContext assemblyContext)
        {
            try
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

                switch (lifetime)
                {
                    case PluginLifetime.Singleton:
                        assemblyContext.ServiceCollection.AddSingleton(type);
                        break;
                    case PluginLifetime.Scoped:
                        assemblyContext.ServiceCollection.AddScoped(type);
                        break;
                    case PluginLifetime.Transient:
                        assemblyContext.ServiceCollection.AddTransient(type);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                string pluginTypeStr;
                if (pluginType == PluginTypes["BasicPlugin`1"] || pluginType == PluginTypes[nameof(BasicPlugin)])
                {
                    pluginTypeStr = "基本";
                }
                else
                {
                    pluginTypeStr = "服务";
                }

                _logger.LogInformation($"{pluginTypeStr}插件 \"{name}\" 已经加载完毕。");

                //string pluginType, error = "", commands = "";

                //if (plugin is IMessagePlugin messagePlugin)
                //{
                //    pluginType = "基本";


                //}
                //else
                //{
                //    pluginType = "服务";
                //}

                //plugin.OnInitialized(startupConfig);
                //AllPluginInitialized += plugin.AllPlugins_Initialized;
                //_logger.LogInformation($"{pluginType} \"{plugin.Name}\" {commands}已经加载完毕。{error}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"加载插件{type.Name}失败。");
            }
        }
    }

    internal class AssemblyContext
    {
        public IServiceCollection ServiceCollection { get; init; }
        public AssemblyLoadContext AssemblyLoadContext { get; init; }
        public Dictionary<string, Assembly> Assemblies { get; init; }

        public ServiceProvider BuildServiceProvider()
        {
            return ServiceCollection.BuildServiceProvider();
        }
    }
}
