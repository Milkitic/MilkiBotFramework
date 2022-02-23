using MilkiBotFramework.Plugins.Loading;

namespace MilkiBotFramework.Plugins;

[PluginLifetime(PluginLifetime.Singleton)]
public abstract class ServicePlugin : PluginBase
{
    public sealed override PluginType PluginType => PluginType.Service;
}