using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Plugining.Configuration;

internal class Configuration<T> : IConfiguration<T> where T : ConfigurationBase
{
    public Configuration(LoaderContext? loaderContext, ConfigurationFactory configurationFactory)
    {
        Instance = configurationFactory.GetConfiguration<T>(loaderContext?.Name ?? "Host");
    }

    public T Instance { get; }
}