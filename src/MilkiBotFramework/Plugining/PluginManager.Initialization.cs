using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Configuration;
using MilkiBotFramework.Plugining.Database;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Plugining;

public partial class PluginManager
{
    //todo: Same command; Same guid
    public async Task InitializeAllPlugins()
    {
        var sw = Stopwatch.StartNew();
        var pluginBaseDir = _botOptions.PluginBaseDir;
        if (!Directory.Exists(pluginBaseDir)) Directory.CreateDirectory(pluginBaseDir);
        var directories = Directory.GetDirectories(pluginBaseDir);

        var entryAsm = Assembly.GetEntryAssembly();
        if (entryAsm != null)
        {
            var dir = Path.GetDirectoryName(entryAsm.Location)!;
            var context = AssemblyLoadContext.Default.Assemblies;
            await CreateContextAndAddPlugins(null, context
                .Where(k => !k.IsDynamic && k.Location.StartsWith(dir))
                .Select(k => k.Location)
            );
        }

        foreach (var directory in directories)
        {
            var files = Directory.GetFiles(directory, "*.dll");
            var contextName = Path.GetFileName(directory);
            await CreateContextAndAddPlugins(contextName, files);
        }

        _logger.LogInformation("Activating singleton plugins...");
        foreach (var loaderContext in _loaderContexts.Values)
        {
            var serviceProvider = loaderContext.BuildServiceProvider();

            foreach (var assemblyContext in loaderContext.AssemblyContexts.Values)
            {
                var failList = new List<PluginInfo>();
                foreach (var pluginInfo in assemblyContext.PluginInfos
                             .Where(o => o.Lifetime == PluginLifetime.Singleton))
                {
                    try
                    {
                        var instance = (PluginBase?)serviceProvider.GetService(pluginInfo.Type);
                        if (instance != null) await InitializePlugin(instance, pluginInfo);
                    }
                    catch (Exception ex)
                    {
                        failList.Add(pluginInfo);
                        _logger.LogError(ex, "Error while initializing plugin " + pluginInfo.Metadata.Name);
                    }
                }

                if (failList.Count == 0) continue;
                foreach (var pluginInfo in failList) pluginInfo.InitializationFailed = true;
            }
        }

        _logger.LogInformation($"Plugin initialization done in {sw.Elapsed.TotalSeconds:N3}s!");
    }

    private async Task CreateContextAndAddPlugins(string? contextName, IEnumerable<string> files)
    {
        var assemblyResults = AssemblyHelper.AnalyzePluginsInAssemblyFilesByDnlib(_logger, files);
        if (assemblyResults.Count <= 0 || assemblyResults.All(k => k.TypeResults.Length == 0))
            return;

        var isRuntimeContext = contextName == null;

        var ctx = !isRuntimeContext
            ? new AssemblyLoadContext(contextName) // No need to hot unload.
            : AssemblyLoadContext.Default;
        var dict = new Dictionary<string, AssemblyContext>();
        var loaderContext = new LoaderContext
        {
            AssemblyLoadContext = ctx,
            ServiceCollection = new ServiceCollection(),
            Name = contextName ?? "Host",
            IsRuntimeContext = isRuntimeContext,
            AssemblyContexts = new ReadOnlyDictionary<string, AssemblyContext>(dict)
        };

        foreach (var assemblyResult in assemblyResults.OrderBy(k => k.TypeResults.Length)) // Load dependency first
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
                Assembly assembly = ctx.LoadFromAssemblyPath(assemblyPath);
                var defaultAuthor = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
                var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "0.0.1-alpha";
                var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()
                    ?.Product;
                _logger.LogInformation($"Plugin library: {product} {version} by " + defaultAuthor);

                var pluginInfos = new List<PluginInfo>();
                foreach (var typeResult in typeResults)
                {
                    var typeFullName = typeResult.TypeFullName!;
                    var baseType = typeResult.BaseType!;
                    string typeName = "";
                    PluginInfo? pluginInfo = null;
                    try
                    {
                        var type = assembly.GetType(typeFullName);
                        if (type == null)
                            throw new Exception("Can't resolve type: " + typeFullName);

                        typeName = type.Name;
                        pluginInfo = GetPluginInfo(type, baseType, defaultAuthor);
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

                        _logger.LogInformation($"Add plugin \"{metadata.Name}\"" +
                                               $" ({pluginInfo.Lifetime} {pluginInfo.BaseType.Name})" +
                                               (defaultAuthor == metadata.Authors
                                                   ? ""
                                                   : $" by {metadata.Authors}"));
                        isValid = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurs while loading plugin: " + typeName);
                    }

                    if (pluginInfo != null)
                    {
                        pluginInfos.Add(pluginInfo);
                        _plugins.Add(pluginInfo);
                    }
                }

                var asmContext = new AssemblyContext
                {
                    Assembly = assembly,
                    DbContextTypes = assemblyResult.DbContexts.Select(dbContext =>
                    {
                        var type = assembly.GetType(dbContext);
                        if (type == null)
                        {
                            Debug.Assert(type != null);
                            _logger.LogError("Cannot resolve DbContext: " + dbContext +
                                             ". This will lead to further errors.");
                        }

                        return type;
                    }).Where(k => k != null!).ToArray(),
                    PluginInfos = pluginInfos.ToArray(),
                    Version = version,
                    Product = product
                };

                if (isValid)
                {
                    dict.Add(assemblyFilename, asmContext);
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

        await InitializeLoaderContext(loaderContext);
    }

    private async Task InitializeLoaderContext(LoaderContext loaderContext)
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
            var ns = serviceDescriptor.ServiceType.Namespace!;
            var fullName = serviceDescriptor.ServiceType.FullName!;
            if (serviceDescriptor.ImplementationType == serviceDescriptor.ServiceType)
            {
                if (ns.StartsWith("Microsoft.Extensions.Options", StringComparison.Ordinal) ||
                    ns.StartsWith("Microsoft.Extensions.Logging", StringComparison.Ordinal))
                    continue;
                if (fullName.Contains("IConfiguration`1"))
                    continue;
                var instance = _serviceProvider.GetService(serviceDescriptor.ImplementationType);
                if (instance == null)
                    loaderContext.ServiceCollection.AddSingleton(serviceDescriptor.ImplementationType, _ => null!);
                else
                    loaderContext.ServiceCollection.AddSingleton(serviceDescriptor.ImplementationType, instance);
            }
            else
            {
                if (ns.StartsWith("Microsoft.Extensions.Options", StringComparison.Ordinal) ||
                    ns.StartsWith("Microsoft.Extensions.Logging", StringComparison.Ordinal))
                    continue;
                if (fullName.Contains("IConfiguration`1"))
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
            loaderContext.ServiceCollection.AddLogging(o => configLoggerProvider.ConfigureLogger(o));

        loaderContext.ServiceCollection.AddSingleton(typeof(IConfiguration<>), typeof(Configuration<>));
        loaderContext.ServiceCollection.AddSingleton(loaderContext);
        foreach (var assemblyContext in loaderContext.AssemblyContexts)
        {
            foreach (var dbContextType in assemblyContext.Value.DbContextTypes)
            {
                var dbFolder =
                    _botOptions.PluginDatabaseDir /*Path.Combine(_botOptions.PluginDatabaseDir, loaderContext.Name)*/;
                var dbFilename =
                    $"{loaderContext.Name}.{Path.GetFileNameWithoutExtension(assemblyContext.Key)}.{dbContextType.Name}.db";
                var dbPath = Path.Combine(dbFolder, dbFilename);
                if (!Directory.Exists(dbFolder)) Directory.CreateDirectory(dbFolder);
                try
                {
                    loaderContext.ServiceCollection.AddScoped(dbContextType, _ =>
                    {
                        var instance = (PluginDbContext)Activator.CreateInstance(dbContextType)!;
                        instance.TemporaryDbPath = dbPath;
                        return instance;
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurs while configuring DbContext: " + dbContextType.FullName);
                }
            }
        }

        var serviceProvider = loaderContext.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        foreach (var assemblyContext in loaderContext.AssemblyContexts)
        {
            foreach (var dbContextType in assemblyContext.Value.DbContextTypes)
            {
                var dbContext = (PluginDbContext)scope.ServiceProvider.GetService(dbContextType)!;
                try
                {
                    _logger.LogInformation("Migrating database: " + dbContextType);
                    var sw = Stopwatch.StartNew();
                    await dbContext.Database.MigrateAsync();
                    await dbContext.Database.CloseConnectionAsync();
                    _logger.LogInformation($"Done {dbContextType}'s migration in {sw.ElapsedMilliseconds}ms");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Fail to migrate DbContext: " + dbContextType.FullName);
                }
            }
        }

        _loaderContexts.Add(loaderContext.Name, loaderContext);
    }

    private async Task InitializePlugin(PluginBase instance, PluginInfo pluginInfo)
    {
        instance.Metadata = pluginInfo.Metadata;
        instance.PluginHome = pluginInfo.PluginHome;
        instance.IsInitialized = true;
        await instance.OnInitialized();
    }

    private PluginInfo GetPluginInfo(Type type, Type baseType, string? defaultAuthor)
    {
        PluginLifetime lifetime;
        if (baseType == StaticTypes.ServicePlugin)
        {
            lifetime = PluginLifetime.Singleton;
        }
        else
        {
            lifetime = type.GetCustomAttribute<PluginLifetimeAttribute>()?.Lifetime ??
                       throw new ArgumentNullException(nameof(PluginLifetimeAttribute.Lifetime),
                           "The plugin lifetime is undefined: " + type.FullName);
        }

        var identifierAttribute = type.GetCustomAttribute<PluginIdentifierAttribute>() ??
                                  throw new Exception("The plugin identifier is undefined: " + type.FullName);
        var guid = identifierAttribute.Guid;
        var index = identifierAttribute.Index;
        var name = ReplaceVariable(identifierAttribute.Name) ?? type.Name;
        var allowDisable = identifierAttribute.AllowDisable;
        var description = ReplaceVariable(type.GetCustomAttribute<DescriptionAttribute>()?.Description);
        var scope = identifierAttribute.Scope ?? type.Assembly.GetName().Name ?? "DynamicScope";
        var authors = identifierAttribute.Authors ?? defaultAuthor ?? "anonym";

        var metadata = new PluginMetadata(Guid.Parse(guid), name, description, authors, scope);

        var pluginHome = Path.Combine(_botOptions.PluginHomeDir, $"{metadata.Guid:B}");
        if (!Directory.Exists(pluginHome))
            Directory.CreateDirectory(pluginHome);

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
                        returnType = CommandReturnType.Unknown;
                }
                else
                    returnType = CommandReturnType.Unknown;
            }

            var commandInfo = new CommandInfo(command, methodDescription, methodInfo, returnType,
                commandHandlerAttribute.Authority, commandHandlerAttribute.AllowedMessageType,
                parameterInfos.ToArray());

            commands.Add(command, commandInfo);
        }

        return new PluginInfo(metadata, type, baseType, lifetime, new ReadOnlyDictionary<string, CommandInfo>(commands),
            index, pluginHome, allowDisable);
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
                parameterInfo.Authority = option.Authority;
                isReady = true;
            }
            else if (attr is ArgumentAttribute argument)
            {
                parameterInfo.DefaultValue = parameter.DefaultValue == DBNull.Value
                    ? argument.DefaultValue
                    : parameter.DefaultValue;

                parameterInfo.IsArgument = true;
                parameterInfo.ValueConverter = _commandLineAnalyzer.DefaultParameterConverter;
                parameterInfo.Authority = argument.Authority;
                isReady = true;
            }
            else if (attr is DescriptionAttribute description)
            {
                parameterInfo.Description = ReplaceVariable(description.Description);
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

    private string? ReplaceVariable(string? content)
    {
        if (content == null) return content;
        if (_botOptions.Variables.Count > 0)
        {
            foreach (var (key, value) in _botOptions.Variables)
            {
                content = content.Replace($"${{{key}}}", value);
            }
        }

        return content;
    }
}