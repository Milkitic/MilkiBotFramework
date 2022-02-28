using System;
using System.Net;
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
                k.BindingUri = connection.ServerBindUri;
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
                .UseContractsManager<GoCqContactsManager>()
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
                .UseContractsManager<GoCqContactsManager>()
                .UseMessageApi<GoCqApi>();
        }
    }

    public class GoCqConnection
    {
        private GoCqConnection(ConnectionType connectionType, string? targetUri, string? serverBindUri)
        {
            ConnectionType = connectionType;
            TargetUri = targetUri;
            ServerBindUri = serverBindUri;
        }

        public ConnectionType ConnectionType { get; private set; }
        public string? TargetUri { get; private set; }
        public string? ServerBindUri { get; private set; }

        public static GoCqConnection Websocket(string callingUri)
        {
            return new GoCqConnection(ConnectionType.Websocket, callingUri, null);
        }

        public static GoCqConnection ReverseWebsocket(string serverBindUri)
        {
            return new GoCqConnection(ConnectionType.ReverseWebsocket, null, serverBindUri);
        }

        public static GoCqConnection Http(string callingUri, string serverBindUri)
        {
            return new GoCqConnection(ConnectionType.Http, callingUri, serverBindUri);
        }
    }
}
