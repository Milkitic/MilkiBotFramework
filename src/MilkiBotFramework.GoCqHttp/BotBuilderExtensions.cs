using Microsoft.Extensions.DependencyInjection;
using MilkiBotFramework.GoCqHttp.Connecting;
using MilkiBotFramework.GoCqHttp.ContractsManaging;
using MilkiBotFramework.GoCqHttp.Dispatching;

namespace MilkiBotFramework.GoCqHttp
{
    public static class BotBuilderExtensions
    {
        public static BotBuilder UseGoCqHttp(this BotBuilder builder, string uri)
        {
            return builder
                .UseConnector<GoCqWsClient>(uri)
                .UseDispatcher<GoCqDispatcher>()
                .UseContractsManager<GoCqContractsManager>()
                .ConfigureServices(k =>
                {
                    k.AddSingleton<GoCqApi>();
                });
        }
    }
}
