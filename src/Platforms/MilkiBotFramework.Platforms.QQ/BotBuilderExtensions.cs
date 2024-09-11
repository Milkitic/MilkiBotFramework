﻿using Microsoft.Extensions.DependencyInjection;
using MilkiBotFramework.Platforms.QQ.Connecting;
using MilkiBotFramework.Platforms.QQ.ContactsManaging;
using MilkiBotFramework.Platforms.QQ.Dispatching;
using MilkiBotFramework.Platforms.QQ.Messaging;
using MilkiBotFramework.Plugining.CommandLine;

namespace MilkiBotFramework.Platforms.QQ;

public static class BotBuilderExtensions
{
    public static TBuilder UseQQ<TBot, TBuilder>(this BotBuilderBase<TBot, TBuilder> builder,
        QConnection? connection = null)
        where TBot : Bot where TBuilder : BotBuilderBase<TBot, TBuilder>
    {
        builder
            .ConfigureServices(k => { k.AddScoped(typeof(QMessageContext)); })
            .UseCommandLineAnalyzer<CommandLineAnalyzer>(new QParameterConverter())
            .UseContactsManager<QContactsManager>()
            .UseDispatcher<QDispatcher>()
            .UseMessageApi<QApi>()
            .UseOptions<QQBotOptions>(null)
            .UseRichMessageConverter<QMessageConverter>();

        connection ??= ((QQBotOptions)builder.GetOptionInstance()).Connection;
        builder.UseConnector<QApiConnector>(k => k.Connection = connection);

        return (TBuilder)builder;
    }
}