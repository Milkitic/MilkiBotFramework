using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.ContractsManaging;
using MilkiBotFramework.Dispatching;
using MilkiBotFramework.Tasking;

namespace MilkiBotFramework.GoCqHttp.ContractsManaging;

public sealed class GoCqContractsManager : ContractsManagerBase
{
    public GoCqContractsManager(BotTaskScheduler botTaskScheduler, ILogger<GoCqContractsManager> logger)
        : base(botTaskScheduler, logger)
    {
    }
}