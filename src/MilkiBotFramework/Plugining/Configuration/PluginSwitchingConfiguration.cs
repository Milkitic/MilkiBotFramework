using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining.Configuration;

public sealed class PluginSwitchingConfiguration : ConfigurationBase
{
    public Dictionary<string, Dictionary<string, bool>> PluginStates { get; set; } = new();

    public bool IsPluginEnabled(MessageIdentity? identity, PluginInfo pluginInfo)
    {
        if (!pluginInfo.AllowDisable) return true;
        if (identity == null) return pluginInfo.DefaultEnabled;

        var identityKey = GetIdentityKey(identity);
        var pluginKey = GetPluginKey(pluginInfo);
        var pluginStates = GetPluginStates();
        return pluginStates.TryGetValue(identityKey, out var states) &&
               states != null &&
               states.TryGetValue(pluginKey, out var enabled)
            ? enabled
            : pluginInfo.DefaultEnabled;
    }

    public bool? GetConfiguredState(MessageIdentity identity, PluginInfo pluginInfo)
    {
        var identityKey = GetIdentityKey(identity);
        var pluginKey = GetPluginKey(pluginInfo);
        var pluginStates = GetPluginStates();
        return pluginStates.TryGetValue(identityKey, out var states) &&
               states != null &&
               states.TryGetValue(pluginKey, out var enabled)
            ? enabled
            : null;
    }

    public void SetPluginEnabled(MessageIdentity identity, PluginInfo pluginInfo, bool enabled)
    {
        if (!pluginInfo.AllowDisable) return;

        var identityKey = GetIdentityKey(identity);
        var pluginKey = GetPluginKey(pluginInfo);
        var pluginStates = GetPluginStates();
        if (enabled == pluginInfo.DefaultEnabled)
        {
            if (!pluginStates.TryGetValue(identityKey, out var states) || states == null) return;

            states.Remove(pluginKey);
            if (states.Count == 0) pluginStates.Remove(identityKey);
            return;
        }

        if (!pluginStates.TryGetValue(identityKey, out var currentStates) || currentStates == null)
        {
            currentStates = new Dictionary<string, bool>();
            pluginStates[identityKey] = currentStates;
        }

        currentStates[pluginKey] = enabled;
    }

    private static string GetIdentityKey(MessageIdentity identity) => identity.ToString();

    private static string GetPluginKey(PluginInfo pluginInfo) => pluginInfo.Metadata.Guid.ToString("D");

    private Dictionary<string, Dictionary<string, bool>> GetPluginStates()
    {
        return PluginStates ??= new Dictionary<string, Dictionary<string, bool>>();
    }
}
