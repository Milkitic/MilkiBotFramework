using MilkiBotFramework;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Platforms.GoCqHttp;

var bot = new AspnetcoreBotBuilder(args)
    .UseGoCqHttp("ws://127.0.0.1:6700")
    .Build();
await bot.RunAsync();