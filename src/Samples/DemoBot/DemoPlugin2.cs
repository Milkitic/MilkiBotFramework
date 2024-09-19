// ReSharper disable All
#pragma warning disable CS1998
#nullable disable

using Microsoft.Extensions.Logging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Configuration;

namespace DemoBot;

[PluginIdentifier("487ade0a-3afb-4451-8543-78e63bd2c668")]
public class DemoPlugin2 : BasicPlugin
{
    private readonly ILogger<DemoPlugin2> _logger;

    public DemoPlugin2(ILogger<DemoPlugin2> logger, IConfiguration<TestConfiguration> configuration)
    {
        _logger = logger;
        //var config = configuration.Instance;
        //config.Key2++;
        //config.SaveAsync().Wait();
    }

    //protected override async Task OnInitialized()
    //{
    //    _logger.LogDebug(nameof(OnInitialized));
    //}

    //protected override async Task OnUninitialized()
    //{
    //    _logger.LogDebug(nameof(OnUninitialized));
    //}

    //protected override async Task OnExecuting()
    //{
    //    _logger.LogDebug(nameof(OnExecuting));
    //}

    //protected override async Task OnExecuted()
    //{
    //    _logger.LogDebug(nameof(OnExecuted));
    //}
}