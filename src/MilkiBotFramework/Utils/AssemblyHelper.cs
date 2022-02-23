using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Plugining;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace MilkiBotFramework.Utils;

internal static class AssemblyHelper
{
    public static readonly Dictionary<string, Type> PluginTypes = new()
    {
        ["BasicPlugin`1"] = typeof(BasicPlugin<>),
        [nameof(BasicPlugin)] = typeof(BasicPlugin),
        [nameof(ServicePlugin)] = typeof(ServicePlugin)
    };

    public static Dictionary<string, (TypeDef, Type)[]> AnalyzePluginsInAssemblyFilesByDnlib(
        ILogger logger,
        params string[] assemblyFiles)
    {
        var availableDictionary = new Dictionary<string, (TypeDef, Type)[]>();

        foreach (var asmPath in assemblyFiles)
        {
            AnalyzeSingle(logger, asmPath, availableDictionary);
        }

        return availableDictionary;
    }

    private static void AnalyzeSingle(ILogger logger, string asmPath, IDictionary<string, (TypeDef, Type)[]> availableDictionary)
    {
        var asmName = Path.GetFileName(asmPath);
        var modCtx = ModuleDef.CreateModuleContext();
        try
        {
            logger.LogInformation("Find " + asmName);
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
                logger.LogWarning($"\"{asmName}\" has no valid classes.");
            availableDictionary.Add(module.Location, types);
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to read assembly {asmName}: {ex.Message}");
        }
    }
}