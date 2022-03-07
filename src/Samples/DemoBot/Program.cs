using Microsoft.Extensions.Logging;
using MilkiBotFramework;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Platforms.GoCqHttp;

var bot =
    //new BotBuilder().UseGoCqHttp("http://0.0.0.0:23333/connector/reverse-ws", false)
    //new BotBuilder().UseGoCqHttp("http://127.0.0.1:5700")
    new AspnetcoreBotBuilder(args, "http://0.0.0.0:23333")
        //.UseGoCqHttp(GoCqConnection.Http("http://127.0.0.1:5700", "/connector"))
        .UseGoCqHttp(GoCqConnection.ReverseWebSocket("/connector/reverse-ws"))
        //.UseGoCqHttp(GoCqConnection.WebSocket("ws://127.0.0.1:6700"))
        .ConfigureLogger(builder =>
        {
            builder
                .AddSimpleConsole(options =>
                {
                    options.IncludeScopes = true;
                    //options.SingleLine = true;
                    options.TimestampFormat = "hh:mm:ss.ffzz ";
                });
            builder.AddFilter((ns, level) =>
            {
#if !DEBUG
                if (ns.StartsWith("Microsoft") && level < LogLevel.Warning)
                    return false;
                if (level < LogLevel.Information)
                    return false;
                return true;
#else
                if (ns.StartsWith("Microsoft") && level < LogLevel.Information)
                    return false;
                return true;
#endif
            });
        })
        .Build();
await bot.RunAsync();