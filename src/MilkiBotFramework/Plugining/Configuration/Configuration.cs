using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining.Configuration;

internal class Configuration<T> : IConfiguration<T> where T : ConfigurationBase
{
    public Configuration(ConfigurationFactory configurationFactory)
    {
        Instance = configurationFactory.GetConfiguration<T>("Host");
    }

    public T Instance { get; }
}

internal class PluginConfiguration<T> : IConfiguration<T> where T : ConfigurationBase
{
    public PluginConfiguration(LoaderContext loaderContext, ConfigurationFactory configurationFactory)
    {
        Instance = configurationFactory.GetConfiguration<T>(loaderContext.Name);
    }

    public T Instance { get; }
}