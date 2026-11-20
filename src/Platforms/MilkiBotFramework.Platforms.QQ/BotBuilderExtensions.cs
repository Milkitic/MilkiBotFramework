using Microsoft.Extensions.DependencyInjection;
using MilkiBotFramework.Platforms.QQ.Connecting;
using MilkiBotFramework.Platforms.QQ.ContactsManaging;
using MilkiBotFramework.Platforms.QQ.Dispatching;
using MilkiBotFramework.Platforms.QQ.Messaging;
using MilkiBotFramework.Platforms.QQ.ObjectStore;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Platforms.QQ;

/// <summary>为机器人构建器添加 QQ 官方平台支持。</summary>
public static class BotBuilderExtensions
{
    /// <summary>注册 QQ 官方 Webhook 连接器、OpenAPI 客户端、消息分发和图片存储。</summary>
    /// <typeparam name="TBot">要构建的机器人类型。</typeparam>
    /// <typeparam name="TBuilder">构建器类型。</typeparam>
    /// <param name="builder">承载 QQ 适配器的 ASP.NET Core 机器人构建器。</param>
    /// <param name="connection">可选的接入配置。省略时使用 <see cref="QQBotOptions.Connection"/>。</param>
    /// <returns>用于继续配置的构建器。</returns>
    public static TBuilder UseQQ<TBot, TBuilder>(this BotBuilderBase<TBot, TBuilder> builder,
        QConnection? connection = null)
        where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
    {
        builder
            .ConfigureServices(k =>
            {
                k.AddScoped(typeof(QMessageContext));
            })
            .UseCommandLineAnalyzer<CommandLineAnalyzer>(new DefaultParameterConverter())
            .UseContactsManager<QContactsManager>()
            .UseDispatcher<QDispatcher>()
            .UseMessageApi<QApi>()
            .UseOptions<QQBotOptions>(null)
            .UseRichMessageConverter<QMessageConverter>();

        var options = (QQBotOptions)builder.GetOptionInstance();
        options.Connection = connection ?? options.Connection;
        builder.UseConnector<QApiConnector>(k =>
        {
            k.Connection = options.Connection;
        });

        var ossOptions = options.OssOptions;

        builder
            .ConfigureServices(k =>
            {
                if (ossOptions.OssType == OssType.MinIO)
                {
                    k.AddSingleton<IObjectStorageProvider, MinIOProvider>();
                }
                else if (ossOptions.OssType == OssType.Qiniu)
                {
                    k.AddSingleton<IObjectStorageProvider, QiniuProvider>();
                }
            });

        return (TBuilder)builder;
    }
}
