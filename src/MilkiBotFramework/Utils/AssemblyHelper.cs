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
        public string? TypeFullName { get; init; }
        public Type? BaseType { get; init; }
    }

    public static readonly Dictionary<string, Type> PluginTypes = new()
    {
        ["BasicPlugin`1"] = StaticTypes.BasicPlugin_,
        [nameof(BasicPlugin)] = StaticTypes.BasicPlugin,
        [nameof(ServicePlugin)] = StaticTypes.ServicePlugin
    };

    public static IReadOnlyList<AssemblyResult> AnalyzePluginsInAssemblyFilesByDnlib(
        ILogger logger,
        IEnumerable<string> assemblyFiles)
    {
        var first = assemblyFiles.FirstOrDefault();
        if (first == null)
            return Array.Empty<AssemblyResult>();
        var folder = Path.GetFileName(Path.GetDirectoryName(first));
        //using var scope = logger.BeginScope($"Quick search in directory \"{folder}\"");
        var availableDictionary = new List<AssemblyResult>();

        foreach (var asmPath in assemblyFiles)
        {
            AnalyzeSingle(logger, asmPath, folder, availableDictionary);
        }

        return availableDictionary;
    }

    private static void AnalyzeSingle(ILogger logger, string asmPath, string? folder,
        ICollection<AssemblyResult> availableDictionary)
    {
        var asmName = Path.GetFileName(asmPath);
        var modCtx = ModuleDef.CreateModuleContext();
        try
        {
            //logger.LogDebug("Find " + asmName);
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
                logger.LogDebug($"Found \"{folder}/{asmName}\" with no plugins.");
            else
                logger.LogInformation($"Found \"{folder}/{asmName}\" with {typeResults.Length} plugin{(typeResults.Length == 1 ? "" : "s")}.");
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