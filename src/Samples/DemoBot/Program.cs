using Microsoft.Extensions.Logging;
using MilkiBotFramework;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Platforms.GoCqHttp;

var bot = new AspnetcoreBotBuilder(args)
    .UseGoCqHttp(GoCqConnection.Websocket("ws://127.0.0.1:6700"))
    .ConfigureLogger(builder =>
    {
        builder
            .AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                //options.SingleLine = true;
                options.TimestampFormat = "hh:mm:ss.ffzz ";
            })
            .AddFilter(_ => true);
    })
    .Build();
await bot.RunAsync();