using Microsoft.Extensions.DependencyInjection;
using MilkiBotFramework.Platforms.Mock.Connecting;
using MilkiBotFramework.Platforms.Mock.ContactsManaging;
using MilkiBotFramework.Platforms.Mock.Dispatching;
using MilkiBotFramework.Platforms.Mock.Messaging;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Platforms.Mock;

/// <summary>
///     Mock 平台 Bot 构建扩展 - 一行代码快速配置 Mock 平台
/// </summary>
public static class BotBuilderExtensions
{
    /// <summary>
    ///     使用 Mock 平台进行本地测试
    /// </summary>
    public static TBuilder UseMock<TBot, TBuilder>(this BotBuilderBase<TBot, TBuilder> builder)
        where TBot : Bot
        where TBuilder : BotBuilderBase<TBot, TBuilder>
    {
        builder
            .ConfigureServices(k => k.AddScoped(typeof(MockMessageContext)))
            .UseCommandLineAnalyzer<CommandLineAnalyzer>(new DefaultParameterConverter())
            .UseContactsManager<MockContactsManager>()
            .UseDispatcher<MockDispatcher>()
            .UseMessageApi<MockMessageApi>()
            .UseOptions<MockBotOptions>(null)
            .UseRichMessageConverter<MockMessageConverter>()
            .UseConnector<MockConnector>();

        return (TBuilder)builder;
    }
}