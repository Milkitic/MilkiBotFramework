using MilkiBotFramework.Plugining.Attributes;

namespace MilkiBotFramework.Plugining;

[PluginLifetime(PluginLifetime.Singleton)]
public abstract class ServicePlugin : PluginBase
{
    public sealed override PluginType PluginType => PluginType.Service;
}