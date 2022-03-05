using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Plugining.Loading;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Plugining.Configuration
{
    public class ConfigurationFactory
    {
        private readonly Dictionary<Type, ConfigurationBase> _cachedDict = new();

        private readonly LoaderContext _loaderContext;
        private readonly BotOptions _botOptions;
        private readonly ILogger<ConfigLoggerProvider> _logger;

        public ConfigurationFactory(LoaderContext loaderContext,
            BotOptions botOptions,
            ILogger<ConfigLoggerProvider> logger)
        {
            _loaderContext = loaderContext;
            _botOptions = botOptions;
            _logger = logger;
        }

        public T GetConfiguration<T>(YamlConverter? converter = null) where T : ConfigurationBase
        {
            var t = typeof(T);

            if (_cachedDict.TryGetValue(t, out var val))
                return (T)val;

            var filename = t.FullName + ".yaml";
            var folder = Path.Combine(_botOptions.PluginConfigurationDir, _loaderContext.Name);
            var path = Path.Combine(folder, filename);
            converter ??= new YamlConverter();
            var success = TryLoadConfigFromFile<T>(path, converter, out var config, out var ex);
            if (!success) throw ex!;
            config!.SaveAction = async () => SaveConfig(config, path, converter);
            _cachedDict.Add(t, config);
            return config;
        }

        public bool TryLoadConfigFromFile<T>(string path,
            YamlConverter converter,
            [NotNullWhen(true)] out T? config,
            [NotNullWhen(false)] out Exception? e)
            where T : ConfigurationBase
        {
            if (!Path.IsPathRooted(path))
                path = Path.Combine(Environment.CurrentDirectory, path);

            if (!File.Exists(path))
            {
                config = CreateDefaultConfigByPath<T>(path, converter);
                _logger.LogWarning($"{Utilities.GetRelativePath(path)} config file not found. " +
                                   $"Default config was created and used.");
            }
            else
            {
                var content = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(content)) content = "default:\r\n";
                try
                {
                    config = converter.DeserializeSettings<T>(content);
                    SaveConfig(config, path, converter);
                    _logger.LogInformation($"{Utilities.GetRelativePath(path)} config file was loaded.");
                }
                //catch (YamlException ex)
                //{
                //    _logger.LogError(ex,
                //        $"Deserialization error occurs while loading {Utilities.GetRelativePath(path)} config file. " +
                //        $"Default config was used."
                //    );
                //    config = converter.DeserializeSettings<T>("");
                //    e = ex;
                //    return true;
                //}
                catch (Exception ex)
                {
                    config = null;
                    e = ex;
                    return false;
                }
            }

            e = null;
            return true;
        }

        public static T CreateDefaultConfigByPath<T>(string path, YamlConverter converter)
            where T : ConfigurationBase
        {
            var dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, "");
            var config = converter.DeserializeSettings<T>("default:\r\n");
            SaveConfig(config, path, converter);
            return config;
        }

        private static void SaveConfig<T>(T config, string path, YamlConverter converter) where T : ConfigurationBase
        {
            var content = converter.SerializeSettings(config);
            File.WriteAllText(path, content, config.Encoding);
        }
    }
}
