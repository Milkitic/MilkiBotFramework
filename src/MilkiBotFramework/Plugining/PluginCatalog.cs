using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using Autofac;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Configuration;
using MilkiBotFramework.Plugining.Database;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Plugining;

public class PluginCatalog
{
    private readonly ILifetimeScope _rootLifetimeScope;
    private readonly ILogger<PluginCatalog> _logger;
    private readonly BotOptions _botOptions;

    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly Dictionary<string, LoaderContext> _loaderContexts = new();
    private readonly HashSet<PluginInfo> _plugins = new();

    private PluginDescriptor[] _executionPlan = Array.Empty<PluginDescriptor>();
    private bool _disposed;

    public PluginCatalog(ILifetimeScope rootLifetimeScope,
        ILogger<PluginCatalog> logger,
        BotOptions botOptions)
    {
        _rootLifetimeScope = rootLifetimeScope;
        _botOptions = botOptions;
        _logger = logger;
    }

    public bool IsInitialized { get; private set; }

    public IReadOnlyList<PluginInfo> GetAllPlugins()
    {
        return _plugins.ToArray();
    }

    public async Task InitializeAllPlugins()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await DisposeLoaderContextsCoreAsync();
            await InitializeAllPluginsCoreAsync();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task ReloadAllPluginsAsync()
    {
        await InitializeAllPlugins();
    }

    public async ValueTask DisposeAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            await DisposeLoaderContextsCoreAsync();
            _disposed = true;
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    internal event Action? ExecutionPlanChanged;

    internal IReadOnlyList<PluginDescriptor> GetExecutionPlan()
    {
        return _executionPlan;
    }

    internal async Task InitializePluginAsync(PluginBase instance, PluginInfo pluginInfo)
    {
        instance.Metadata = pluginInfo.Metadata;
        instance.PluginHome = pluginInfo.PluginHome;
        instance.IsInitialized = true;
        await instance.OnInitialized();
    }

    private void RebuildExecutionPlan()
    {
        _executionPlan = _loaderContexts.Values
            .SelectMany(loaderContext => loaderContext.AssemblyContexts.Values
                .SelectMany(assemblyContext => assemblyContext.PluginInfos
                    .Select(pluginInfo => new PluginDescriptor(loaderContext, pluginInfo))))
            .OrderBy(descriptor => descriptor.PluginInfo.Index)
            .ToArray();
        ExecutionPlanChanged?.Invoke();
    }

    private async Task InitializeAllPluginsCoreAsync()
    {
        IsInitialized = false;
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

        RebuildExecutionPlan();

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
                        var instance = ResolvePluginInstance(serviceProvider, pluginInfo);
                        if (instance != null) await InitializePluginAsync(instance, pluginInfo);
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

        IsInitialized = true;
        _logger.LogInformation($"Plugin initialization done in {sw.Elapsed.TotalSeconds:N3}s!");
    }

    private async Task DisposeLoaderContextsCoreAsync()
    {
        IsInitialized = false;

        var executionPlan = _executionPlan;
        var loaderContexts = _loaderContexts.Values.ToArray();

        _executionPlan = Array.Empty<PluginDescriptor>();
        _plugins.Clear();
        _loaderContexts.Clear();
        ExecutionPlanChanged?.Invoke();

        await UninitializeSingletonPluginsAsync(executionPlan);

        foreach (var loaderContext in loaderContexts)
        {
            await loaderContext.DisposeAsync();
        }
    }

    private async Task UninitializeSingletonPluginsAsync(IEnumerable<PluginDescriptor> executionPlan)
    {
        foreach (var descriptor in executionPlan)
        {
            var pluginInfo = descriptor.PluginInfo;
            if (pluginInfo.Lifetime != PluginLifetime.Singleton || pluginInfo.InitializationFailed)
            {
                continue;
            }

            try
            {
                var serviceProvider = descriptor.LoaderContext.BuildServiceProvider();
                var instance = ResolvePluginInstance(serviceProvider, pluginInfo);
                if (instance is not { IsInitialized: true })
                {
                    continue;
                }

                await instance.OnUninitialized();
                instance.IsInitialized = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while uninitializing plugin " + pluginInfo.Metadata.Name);
            }
        }
    }

    private static PluginBase? ResolvePluginInstance(IServiceProvider serviceProvider, PluginInfo pluginInfo)
    {
        var instance = serviceProvider.GetService(pluginInfo.Type) as PluginBase;
        if (instance != null)
        {
            return instance;
        }

        return pluginInfo.ServiceType == null
            ? null
            : serviceProvider.GetService(pluginInfo.ServiceType) as PluginBase;
    }

    private async Task CreateContextAndAddPlugins(string? contextName, IEnumerable<string> files)
    {
        var assemblyFiles = files as string[] ?? files.ToArray();
        var assemblyResults = AssemblyHelper.AnalyzePluginsInAssemblyFilesByDnlib(_logger, assemblyFiles);
        if (assemblyResults.Count <= 0 || assemblyResults.All(k => k.TypeResults.Length == 0))
            return;

        var isRuntimeContext = contextName == null;

        var ctx = !isRuntimeContext
            ? new PluginAssemblyLoadContext(contextName!, assemblyFiles)
            : AssemblyLoadContext.Default;
        var dict = new Dictionary<string, AssemblyContext>();
        var loaderContext = new LoaderContext
        {
            AssemblyLoadContext = ctx,
            ServiceCollection = new ServiceCollection(),
            HostLifetimeScope = _rootLifetimeScope,
            Name = contextName ?? "Host",
            IsRuntimeContext = isRuntimeContext,
            AssemblyContexts = new ReadOnlyDictionary<string, AssemblyContext>(dict)
        };

        foreach (var assemblyResult in assemblyResults.OrderBy(k => k.TypeResults.Length))
        {
            var assemblyPath = assemblyResult.AssemblyPath;
            var assemblyFilename = Path.GetFileName(assemblyPath);
            var typeResults = assemblyResult.TypeResults;

            if (typeResults.Length == 0)
            {
                continue;
            }

            var isValid = false;

            try
            {
                var assembly = ctx.LoadFromAssemblyPath(assemblyPath);
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
                        var serviceType = pluginInfo.ServiceType;
                        if (serviceType == null)
                        {
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
                        }
                        else
                        {
                            if (!serviceType.IsAssignableFrom(type))
                            {
                                throw new Exception(
                                    $"The plugin type {type.FullName} does not implement the declaration type {serviceType.FullName}.");
                            }

                            switch (pluginInfo.Lifetime)
                            {
                                case PluginLifetime.Singleton:
                                    loaderContext.ServiceCollection.AddSingleton(serviceType, type);
                                    break;
                                case PluginLifetime.Scoped:
                                    loaderContext.ServiceCollection.AddScoped(serviceType, type);
                                    break;
                                case PluginLifetime.Transient:
                                    loaderContext.ServiceCollection.AddTransient(serviceType, type);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
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
        loaderContext.ServiceCollection.AddSingleton(typeof(IConfiguration<>), typeof(PluginConfiguration<>));
        loaderContext.ServiceCollection.AddSingleton(loaderContext);
        foreach (var assemblyContext in loaderContext.AssemblyContexts)
        {
            foreach (var dbContextType in assemblyContext.Value.DbContextTypes)
            {
                var dbFolder = _botOptions.PluginDatabaseDir;
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
        var serviceType = identifierAttribute.ServiceType;
        var description = ReplaceVariable(type.GetCustomAttribute<DescriptionAttribute>()?.Description);
        var scope = identifierAttribute.Scope ?? type.Assembly.GetName().Name ?? "DynamicScope";
        var authors = identifierAttribute.Authors ?? defaultAuthor ?? "anonym";

        var metadata = new PluginMetadata(Guid.Parse(guid), name, description, authors, scope);

        var pluginHome = _botOptions.PluginDataUseGuid
            ? Path.Combine(_botOptions.PluginDataDir, $"{metadata.Guid:B}")
            : Path.Combine(_botOptions.PluginDataDir, $"{type.Name}");
        if (!Directory.Exists(pluginHome))
            Directory.CreateDirectory(pluginHome);

        var methodSets = new HashSet<string>();
        var commands = new Dictionary<string, CommandInfo>();
        foreach (var methodInfo in type.GetMethods())
        {
            if (!methodSets.Add(methodInfo.Name))
            {
                throw new ArgumentException(
                    "Duplicate method name with CommandHandler definition is not supported.", methodInfo.Name);
            }

            var commandHandlerAttribute = methodInfo.GetCustomAttribute<CommandHandlerAttribute>();
            if (commandHandlerAttribute == null) continue;

            var command = commandHandlerAttribute.Command ?? methodInfo.Name.ToLower();
            var methodDescription = methodInfo.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";

            var parameterInfos = new List<CommandParameterInfo>();
            var parameters = methodInfo.GetParameters();
            foreach (var parameter in parameters)
            {
                var parameterInfo = CommandParameterInfoFactory.CreateForMethodParameter(parameter,
                    DefaultParameterConverter.Instance);
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
            index, pluginHome, allowDisable, serviceType);
    }

    private string? ReplaceVariable(string? text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        foreach (var (key, value) in _botOptions.Variables)
        {
            text = text.Replace($"${{{key}}}", value);
        }

        return text;
    }
}