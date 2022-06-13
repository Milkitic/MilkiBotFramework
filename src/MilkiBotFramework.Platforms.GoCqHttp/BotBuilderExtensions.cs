using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.ContactsManaging;
using MilkiBotFramework.Platforms.GoCqHttp.Dispatching;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Platforms.GoCqHttp;

public static class BotBuilderExtensions
{
    public static TBuilder UseGoCqHttp<TBot, TBuilder>(this BotBuilderBase<TBot, TBuilder> builder,
        GoCqConnection? connection = null)
        where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
    {
        builder
            .ConfigureServices(k => { k.AddScoped(typeof(GoCqMessageContext)); })
            .UseCommandLineAnalyzer<CommandLineAnalyzer>(new GoCqParameterConverter())
            .UseContactsManager<GoCqContactsManager>()
            .UseDispatcher<GoCqDispatcher>()
            .UseMessageApi<GoCqApi>()
            .UseOptions<GoCqBotOptions>(null)
            .UseRichMessageConverter<GoCqMessageConverter>();

        connection ??= ((GoCqBotOptions)builder.GetOptionInstance()).Connection;

        if (builder is AspnetcoreBotBuilder aspBuilder)
        {
            BuildAspnetcoreConnections(builder, connection, aspBuilder);
        }
        else
        {
            BuildCommonConnections(builder, connection);
        }

        return (TBuilder)builder;
    }

    private static void BuildCommonConnections<TBot, TBuilder>(BotBuilderBase<TBot, TBuilder> builder,
        GoCqConnection connection)
        where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
    {
        if (connection.ConnectionType == ConnectionType.WebSocket)
        {
            builder.UseConnector<GoCqClient>(connection.TargetUri ??
                                             throw new ArgumentNullException(nameof(connection.TargetUri)));
        }
        else if (connection.ConnectionType == ConnectionType.ReverseWebSocket)
        {
            builder.UseConnector<GoCqServer>(connection.ServerBindUrl + connection.ServerBindPath);
        }
        else
        {
            throw new NotSupportedException("不支持通常的BotBuilder创建Http通讯，请使用AspnetcoreBotBuilder代替。");
        }
    }

    private static void BuildAspnetcoreConnections<TBot, TBuilder>(BotBuilderBase<TBot, TBuilder> builder,
        GoCqConnection connection,
        AspnetcoreBotBuilder aspBuilder) where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
    {
        if (aspBuilder.BindUrls == AspnetcoreBotBuilder.DefaultUris)
        {
            aspBuilder.UseUrl(connection.ServerBindUrl ??
                              throw new ArgumentNullException(nameof(connection.ServerBindUrl)));
        }

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
    }
}