namespace MilkiBotFramework.Plugining.Configuration;

public interface IConfiguration<out T> where T : ConfigurationBase
{
    public T Instance { get; }
}