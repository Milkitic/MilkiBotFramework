namespace MilkiBotFramework.Plugining.Configuration;

internal class Configuration<T> : IConfiguration<T> where T : ConfigurationBase
{
    public Configuration(ConfigurationFactory configurationFactory)
    {
        Instance = configurationFactory.GetConfiguration<T>();
    }

    public T Instance { get; }
}