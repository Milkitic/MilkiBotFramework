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
    public class AssemblyResult
    {
        public string AssemblyFullName { get; init; }
        public string AssemblyPath { get; init; }
        public TypeResult[] TypeResults { get; init; }
    }

    public class TypeResult
    {
        public string TypeFullName { get; init; }
        public Type BaseType { get; init; }
    }

    public static readonly Dictionary<string, Type> PluginTypes = new()
    {
        ["BasicPlugin`1"] = typeof(BasicPlugin<>),
        [nameof(BasicPlugin)] = typeof(BasicPlugin),
        [nameof(ServicePlugin)] = typeof(ServicePlugin)
    };

    public static List<AssemblyResult> AnalyzePluginsInAssemblyFilesByDnlib(
        ILogger logger,
        params string[] assemblyFiles)
    {
        var availableDictionary = new List<AssemblyResult>();

        foreach (var asmPath in assemblyFiles)
        {
            AnalyzeSingle(logger, asmPath, availableDictionary);
        }

        return availableDictionary;
    }

    private static void AnalyzeSingle(ILogger logger, string asmPath, ICollection<AssemblyResult> availableDictionary)
    {
        var asmName = Path.GetFileName(asmPath);
        var folder = Path.GetFileName(Path.GetDirectoryName(asmPath));
        var modCtx = ModuleDef.CreateModuleContext();
        try
        {
            logger.LogDebug("Find " + asmName + " in ./" + folder);
            using var module = ModuleDefMD.Load(asmPath, modCtx);
            var typeResults = module.HasTypes
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
                    return new TypeResult
                    {
                        BaseType = pluginType,
                        TypeFullName = result?.FullName
                    };
                }).Where(k => k.TypeFullName != null).ToArray()
                : Array.Empty<TypeResult>();
            if (typeResults.Length == 0)
                logger.LogDebug($"\"{folder}/{asmName}\" has no valid classes.");
            availableDictionary.Add(new AssemblyResult
            {
                TypeResults = typeResults,
                AssemblyPath = module.Location,
                AssemblyFullName = module.Assembly.FullName
            });
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to read assembly {asmName}: {ex.Message}");
        }
    }
}