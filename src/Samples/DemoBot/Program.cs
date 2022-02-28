using Microsoft.Extensions.Logging;
using MilkiBotFramework;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Platforms.GoCqHttp;

var bot = new AspnetcoreBotBuilder(args)
    //.UseGoCqHttp(GoCqConnection.Websocket("ws://127.0.0.1:6700"))
    .UseGoCqHttp(GoCqConnection.Http("http://127.0.0.1:5700", ""))
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