using System.Text;
using YamlDotNet.Serialization;

namespace MilkiBotFramework.Plugining.Configuration;

public class ConfigurationBase
{
    [YamlIgnore]
    public virtual Encoding Encoding { get; } = Encoding.UTF8;

    [YamlIgnore]
    internal Func<Task>? AsyncSaveAction;

    [YamlIgnore]
    internal Action? SaveAction;

    public async Task SaveAsync()
    {
        if (AsyncSaveAction != null) await AsyncSaveAction();
    }

    public void Save()
    {
        SaveAction?.Invoke();
    }
}