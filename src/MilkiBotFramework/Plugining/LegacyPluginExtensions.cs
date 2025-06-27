using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;

namespace MilkiBotFramework.Plugining;

public static class LegacyPluginExtensions
{
    public static void SaveSettings<T>(this BasicPlugin plugin, ILogger logger, T cls,
        string? fileName = null, bool writeLog = false)
    {
        var settingsFileName = (fileName ?? typeof(T).Name) + ".json";
        var settingsFilePath = Path.Combine(plugin.PluginHome, "settings", settingsFileName);

        var directory = Path.GetDirectoryName(settingsFilePath);
        if (directory != null)
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(settingsFilePath, JsonSerializer.Serialize(cls, new JsonSerializerOptions()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        }));

        if (writeLog)
        {
            logger.LogInformation($"Saved settings to \"{Path.Combine("~", "settings", settingsFileName)}\".");
        }
    }
    public static T? LoadSettings<T>(this BasicPlugin plugin, ILogger logger,
        string? fileName = null, bool writeLog = false)
    {
        var settingsFileName = (fileName ?? typeof(T).Name) + ".json";
        var settingsFilePath = Path.Combine(plugin.PluginHome, "settings", settingsFileName);

        try
        {
            if (!File.Exists(settingsFilePath))
            {
                return default;
            }

            var directory = Path.GetDirectoryName(settingsFilePath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }

            var json = File.ReadAllText(settingsFilePath);
            var settings = JsonSerializer.Deserialize<T>(json);

            if (writeLog)
            {
                logger.LogInformation($"Loaded settings from \"{Path.Combine("~", "settings", settingsFileName)}\".");
            }

            return settings;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading settings from {FilePath}", settingsFilePath);
            throw;
        }
    }
}