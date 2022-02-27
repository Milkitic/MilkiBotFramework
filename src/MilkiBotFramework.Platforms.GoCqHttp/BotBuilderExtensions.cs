using Microsoft.Extensions.DependencyInjection;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.ContactsManaging;
using MilkiBotFramework.Platforms.GoCqHttp.Dispatching;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Platforms.GoCqHttp
{
    public static class BotBuilderExtensions
    {
        public static TBuilder UseGoCqHttp<TBot, TBuilder>(this BotBuilderBase<TBot, TBuilder> builder, string uri) where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
        {
            return builder
                .ConfigureServices(k =>
                {
                    k.AddScoped(typeof(GoCqMessageContext));
                })
                .UseConnector<GoCqWsClient>(uri)
                .UseDispatcher<GoCqDispatcher>()
                .UseCommandLineAnalyzer<CommandLineAnalyzer>(new GoCqParameterConverter())
                .UseRichMessageConverter<GoCqMessageConverter>()
                .UseContractsManager<GoCqContactsManager>()
                .UseMessageApi<GoCqApi>();
        }
    }
}
