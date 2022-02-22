using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using dnlib.DotNet;
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
            [nameof(BasicPlugin)] = typeof(BasicPlugin),
            [nameof(ServicePlugin)] = typeof(ServicePlugin)
        };

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

        internal async void InitializeAllPlugins()
        {
            var directories = Directory.GetDirectories(PluginBaseDirectory);

            foreach (var directory in directories)
            {
                var availableDictionary = new Dictionary<string, TypeDef[]>();
                var files = Directory.GetFiles(directory, "*.dll");

                foreach (var asmPath in files)
                {
                    var asmName = Path.GetFileName(asmPath);
                    var modCtx = ModuleDef.CreateModuleContext();
                    try
                    {
                        _logger.LogInformation("Find " + asmName);
                        var module = ModuleDefMD.Load(asmPath, modCtx);
                        var types = module.HasTypes
                            ? module.Types.Where(k =>
                            {
                                ITypeDefOrRef baseType = k;
                                while (baseType != null && !PluginTypes.ContainsKey(baseType.Name))
                                {
                                    var typeDef = baseType as TypeDef;
                                    if (typeDef == null)
                                    {
                                        baseType = null;
                                        break;
                                    }

                                    baseType = typeDef.BaseType;
                                }

                                var valid = baseType != null &&
                                            !k.IsAbstract &&
                                            k.IsPublic &&
                                            PluginTypes.ContainsKey(baseType.Name);
                                return valid;
                            }).ToArray()
                            : Array.Empty<TypeDef>();
                        if (types.Length == 0)
                            _logger.LogWarning($"\"{asmName}\" has no valid classes.");
                        availableDictionary.Add(module.Location, types);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to read assembly {asmName}: {ex.Message}");
                    }
                }

                if (availableDictionary.Count <= 0) continue;
                var dirName = Path.GetFileName(directory);
                var ctx = new AssemblyLoadContext(dirName, true);

                foreach (var k in availableDictionary)
                {
                    var (asmPath, typeDefs) = k;
                    var asmName = Path.GetFileName(asmPath);

                    if (k.Value.Length == 0)
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
                        Assembly asm = ctx.LoadFromAssemblyPath(asmPath);
                        foreach (var typeDef in typeDefs)
                        {
                            string typeName = "";
                            try
                            {
                                var type = asm.GetType(typeDef.FullName);
                                typeName = type.Name;
                                InsertPlugin(type, startupConfig);
                                isValid = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, typeName + " 抛出了未处理的异常。");
                            }
                        }

                        if (isValid)
                            Assemblies.Add(new TaggedClass<Assembly>(asmName, asm));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex.Message);
                    }

                    if (!isValid)
                        _logger.LogWarning($"\"{asmName}\" 不是合法的插件扩展。");
                }
            }
        }
    }

    internal class AssemblyContext
    {
        public IServiceProvider ServiceProvider { get; set; }
        public AssemblyLoadContext AssemblyLoadContext { get; set; }
    }
}
