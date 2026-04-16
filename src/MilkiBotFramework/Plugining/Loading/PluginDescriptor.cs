namespace MilkiBotFramework.Plugining.Loading;

internal sealed class PluginDescriptor
{
    public PluginDescriptor(LoaderContext loaderContext, PluginInfo pluginInfo)
    {
        LoaderContext = loaderContext;
        PluginInfo = pluginInfo;
    }

    public LoaderContext LoaderContext { get; }
    public PluginInfo PluginInfo { get; }
}