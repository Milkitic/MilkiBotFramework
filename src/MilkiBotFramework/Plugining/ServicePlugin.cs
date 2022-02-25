using System.Threading.Tasks;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining.Attributes;

namespace MilkiBotFramework.Plugining;

[PluginLifetime(PluginLifetime.Singleton)]
public abstract class ServicePlugin : PluginBase
{
    public sealed override PluginType PluginType => PluginType.Service;
    public virtual Task BeforeSend(IResponse response) => Task.CompletedTask;
}