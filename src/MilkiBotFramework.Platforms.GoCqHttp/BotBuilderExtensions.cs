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
                if (connection.ConnectionType == ConnectionType.WebSocket)
                    k.AddSingleton(typeof(IWebSocketConnector),
                        s => new GoCqClient(s.GetService<ILogger<GoCqClient>>()!)
                        {
                            TargetUri = connection.TargetUri
                        });
                else
                    k.AddSingleton(typeof(IWebSocketConnector), _ => null!);
            });

            return builder
                .ConfigureServices(k => { k.AddScoped(typeof(GoCqMessageContext)); })
                .UseDispatcher<GoCqDispatcher>()
                .UseCommandLineAnalyzer<CommandLineAnalyzer>(new GoCqParameterConverter())
                .UseRichMessageConverter<GoCqMessageConverter>()
                .UseContactsManager<GoCqContactsManager>()
                .UseMessageApi<GoCqApi>();
        }

        public static TBuilder UseGoCqHttp<TBot, TBuilder>(this BotBuilderBase<TBot, TBuilder> builder, 
            string wsUri,
            bool asClient = true)
            where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
        {
            if (asClient)
            {
                builder.UseConnector<GoCqClient>(wsUri);
            }
            else
            {
                builder.UseConnector<GoCqServer>(wsUri);
            }

            return builder
                .ConfigureServices(k => { k.AddScoped(typeof(GoCqMessageContext)); })
                .UseDispatcher<GoCqDispatcher>()
                .UseCommandLineAnalyzer<CommandLineAnalyzer>(new GoCqParameterConverter())
                .UseRichMessageConverter<GoCqMessageConverter>()
                .UseContactsManager<GoCqContactsManager>()
                .UseMessageApi<GoCqApi>();
        }
    }
}
