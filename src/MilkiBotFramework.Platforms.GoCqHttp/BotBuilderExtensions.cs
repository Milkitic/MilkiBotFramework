using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.ContactsManaging;
using MilkiBotFramework.Platforms.GoCqHttp.Dispatching;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Platforms.GoCqHttp
{
    public static class BotBuilderExtensions
    {
        public static AspnetcoreBotBuilder UseGoCqHttp(this AspnetcoreBotBuilder builder, GoCqConnection connection)
        {
            builder.UseConnector<GoCqKestrelConnector>(k =>
            {
                k.TargetUri = connection.TargetUri!;
                k.BindingPath = connection.ServerBindPath;
                k.ConnectionType = connection.ConnectionType;
            });
            builder.ConfigureServices(k =>
            {
                if (connection.ConnectionType == ConnectionType.Websocket)
                    k.AddSingleton(typeof(WebSocketClientConnector),
                        s => new GoCqClient(s.GetService<ILogger<GoCqClient>>()!)
                        {
                            TargetUri = connection.TargetUri
                        });
                else
                    k.AddSingleton(typeof(WebSocketClientConnector), _ => null!);
            });

            return builder
                .ConfigureServices(k => { k.AddScoped(typeof(GoCqMessageContext)); })
                .UseDispatcher<GoCqDispatcher>()
                .UseCommandLineAnalyzer<CommandLineAnalyzer>(new GoCqParameterConverter())
                .UseRichMessageConverter<GoCqMessageConverter>()
                .UseContactsManager<GoCqContactsManager>()
                .UseMessageApi<GoCqApi>();
        }

        public static TBuilder UseGoCqHttp<TBot, TBuilder>(this BotBuilderBase<TBot, TBuilder> builder, string wsServerUri)
            where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
        {
            return builder
                .UseConnector<GoCqClient>(wsServerUri)
                .ConfigureServices(k => { k.AddScoped(typeof(GoCqMessageContext)); })
                .UseDispatcher<GoCqDispatcher>()
                .UseCommandLineAnalyzer<CommandLineAnalyzer>(new GoCqParameterConverter())
                .UseRichMessageConverter<GoCqMessageConverter>()
                .UseContactsManager<GoCqContactsManager>()
                .UseMessageApi<GoCqApi>();
        }
    }
}
