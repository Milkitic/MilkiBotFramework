using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Utils;

namespace MilkiBotFramework.Plugining.Configuration
{
    public class ConfigurationFactory
    {
        private readonly Dictionary<Type, ConfigurationBase> _cachedDict = new();

        private readonly BotOptions _botOptions;
        private readonly ILogger<ConfigLoggerProvider> _logger;

        public ConfigurationFactory(BotOptions botOptions,
            ILogger<ConfigLoggerProvider> logger)
        {
            _botOptions = botOptions;
            _logger = logger;
        }

        public T GetConfiguration<T>(string contextName, YamlConverter? converter = null) where T : ConfigurationBase
        {
            var t = typeof(T);

            if (_cachedDict.TryGetValue(t, out var val))
                return (T)val;

            var filename = $"{contextName}.{t.FullName}.yaml";
            var folder = _botOptions.PluginConfigurationDir/*Path.Combine(_botOptions.PluginConfigurationDir, _loaderContext.Name)*/;
            var path = Path.Combine(folder, filename);
            converter ??= new YamlConverter();
            var success = TryLoadConfigFromFile<T>(path, converter, _logger, out var config, out var ex);
            if (!success) throw ex!;
            config!.SaveAction = async () => SaveConfig(config, path, converter);
            _cachedDict.Add(t, config);
            return config;
        }

        public static bool TryLoadConfigFromFile<T>(
            string path,
            YamlConverter converter,
            ILogger? logger,
            [NotNullWhen(true)] out T? config,
            [NotNullWhen(false)] out Exception? e) where T : ConfigurationBase
        {
            var success = TryLoadConfigFromFile(typeof(T), path, converter, logger, out var config1, out e);
            config = (T?)config1;
            return success;
        }

        public static bool TryLoadConfigFromFile(
            Type type,
            string path,
            YamlConverter converter,
            ILogger? logger,
            [NotNullWhen(true)] out ConfigurationBase? config,
            [NotNullWhen(false)] out Exception? e)
        {
            if (!Path.IsPathRooted(path))
                path = Path.Combine(Environment.CurrentDirectory, path);

            if (!File.Exists(path))
            {
                config = CreateDefaultConfigByPath(type, path, converter);
                logger?.LogWarning($"Config file \"{Path.GetFileName(path)}\" was not found. " +
                                  $"Default config was created and used.");
            }
            else
            {
                var content = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(content)) content = "default:\r\n";
                try
                {
                    config = converter.DeserializeSettings(content, type);
                    SaveConfig(config, path, converter);
                    logger?.LogDebug($"Config file \"{Path.GetFileName(path)}\" was loaded.");
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

        public static ConfigurationBase CreateDefaultConfigByPath(Type type, string path, YamlConverter converter)
        {
            var dir = Path.GetDirectoryName(path);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(path, "");
            var config = converter.DeserializeSettings("default:\r\n", type);
            SaveConfig(config, path, converter);
            return config;
        }

        private static void SaveConfig(ConfigurationBase config, string path, YamlConverter converter)
        {
            var content = converter.SerializeSettings(config);
            File.WriteAllText(path, content, config.Encoding);
        }
    }
}
