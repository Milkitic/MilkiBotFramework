using Microsoft.Extensions.DependencyInjection;
using MilkiBotFramework.Platforms.Discord.Connecting;
using MilkiBotFramework.Platforms.Discord.ContactsManaging;
using MilkiBotFramework.Platforms.Discord.Dispatching;
using MilkiBotFramework.Platforms.Discord.Messaging;
using MilkiBotFramework.Plugining.CommandLine;
using MilkiBotFramework.Plugining.Loading;

namespace MilkiBotFramework.Platforms.Discord;

public static class BotBuilderExtensions
{
    public static TBuilder UseDiscord<TBot, TBuilder>(this BotBuilderBase<TBot, TBuilder> builder,
        string? optionPath = null)
        where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
    {
        builder
            .ConfigureServices(k => { k.AddScoped(typeof(DiscordMessageContext)); })
            .UseCommandLineAnalyzer<CommandLineAnalyzer>(new DefaultParameterConverter())
            .UseContactsManager<DiscordContactsManager>()
            .UseDispatcher<DiscordDispatcher>()
            .UseMessageApi<DiscordMessageApi>()
            .UseOptions<DiscordBotOptions>(optionPath)
            .UseRichMessageConverter<DiscordMessageConverter>();

        builder.UseConnector<DiscordConnector>();

        return (TBuilder)builder;
    }
}