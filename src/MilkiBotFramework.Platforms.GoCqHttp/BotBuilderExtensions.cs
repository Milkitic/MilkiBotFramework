using MilkiBotFramework.Platforms.GoCqHttp.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.ContractsManaging;
using MilkiBotFramework.Platforms.GoCqHttp.Dispatching;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Platforms.GoCqHttp
{
    public static class BotBuilderExtensions
    {
        public static BotBuilder UseGoCqHttp(this BotBuilder builder, string uri)
        {
            return builder
                .UseConnector<GoCqWsClient>(uri)
                .UseDispatcher<GoCqDispatcher>()
                .UseCommandLineAnalyzer<CommandLineAnalyzer>(new GoCqParameterConverter())
                .UseRichMessageConverter<GoCqMessageConverter>()
                .UseContractsManager<GoCqContractsManager>()
                .UseMessageApi<GoCqApi>();
        }
    }
}
