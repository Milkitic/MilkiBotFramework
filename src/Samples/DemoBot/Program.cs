using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.GoCqHttp;
using MilkiBotFramework.GoCqHttp.Connecting;
using MilkiBotFramework.GoCqHttp.ContractsManaging;
using MilkiBotFramework.GoCqHttp.Dispatching;

namespace DemoBot
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var bot = Bot.Create(builder => builder.UseGoCqHttp("ws://127.0.0.1:6700"));
            await bot.RunAsync();
        }
    }
}
