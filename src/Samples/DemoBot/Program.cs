using MilkiBotFramework;
using MilkiBotFramework.Platforms.GoCqHttp;

var bot = Bot.Create(builder => builder.UseGoCqHttp("ws://127.0.0.1:6700"));
await bot.RunAsync();