using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting;
using MilkiBotFramework.Platforms.OneBot.ContactsManaging;
using MilkiBotFramework.Platforms.OneBot.Dispatching;
using MilkiBotFramework.Platforms.OneBot.Messaging;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Platforms.OneBot;

public static class BotBuilderExtensions
{
    public static TBuilder UseOneBot<TBot, TBuilder>(this BotBuilderBase<TBot, TBuilder> builder,
        OneBotConnection? connection = null,
        string? optionPath = null)
        where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
    {
        builder
            .ConfigureServices(k => { k.AddScoped(typeof(OneBotMessageContext)); })
            .UseCommandLineAnalyzer<CommandLineAnalyzer>(new OneBotParameterConverter())
            .UseContactsManager<OneBotContactsManager>()
            .UseDispatcher<OneBotDispatcher>()
            .UseMessageApi<OneBotApi>()
            .UseOptions<OneBotOptions>(optionPath)
            .UseRichMessageConverter<OneBotMessageConverter>();

        connection ??= ((OneBotOptions)builder.GetOptionInstance()).Connection;

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
        OneBotConnection connection)
        where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
    {
        if (connection.ConnectionType == ConnectionType.WebSocket)
        {
            builder.UseConnector<OneBotClient>(connection.TargetUri ??
                                             throw new ArgumentNullException(nameof(connection.TargetUri)));
        }
        else if (connection.ConnectionType == ConnectionType.ReverseWebSocket)
        {
            builder.UseConnector<OneBotServer>(connection.ServerBindUrl + connection.ServerBindPath);
        }
        else
        {
            throw new NotSupportedException("不支持通常的BotBuilder创建Http通讯，请使用AspnetcoreBotBuilder代替。");
        }
    }

    private static void BuildAspnetcoreConnections<TBot, TBuilder>(BotBuilderBase<TBot, TBuilder> builder,
        OneBotConnection connection,
        AspnetcoreBotBuilder aspBuilder) where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
    {
        if (aspBuilder.BindUrls == AspnetcoreBotBuilder.DefaultUris)
        {
            aspBuilder.UseUrl(connection.ServerBindUrl ??
                              throw new ArgumentNullException(nameof(connection.ServerBindUrl)));
        }

        builder.UseConnector<OneBotKestrelConnector>(k =>
        {
            k.TargetUri = connection.TargetUri!;
            k.BindingPath = connection.ServerBindPath;
            k.ConnectionType = connection.ConnectionType;
        });
        builder.ConfigureServices(k =>
        {
            if (connection.ConnectionType == ConnectionType.WebSocket)
            {
                k.AddSingleton(typeof(IWebSocketConnector),
                    s => new OneBotClient(s.GetService<ILogger<OneBotClient>>()!)
                    {
                        TargetUri = connection.TargetUri
                    });
            }
        });
    }
}