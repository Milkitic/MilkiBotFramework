using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.GoCqHttp.Connecting;
using MilkiBotFramework.GoCqHttp.ContractsManaging;
using MilkiBotFramework.GoCqHttp.Dispatching;

namespace DemoBot
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var bot = Bot.Create(builder => builder
                .ConfigureLogger(k =>
                    k.AddSimpleConsole(options =>
                    {
                        options.IncludeScopes = true;
                        //options.SingleLine = true;
                        options.TimestampFormat = "hh:mm:ss.ffzz ";
                    }))
                .UseSingleton<GoCqApi>()
                .UseConnector<GoCqWsClient>("ws://127.0.0.1:6700")
                .UseDispatcher<GoCqDispatcher>()
                .UseContractsManager<GoCqContractsManager>()
            );

            await bot.RunAsync();
        }
    }
}
