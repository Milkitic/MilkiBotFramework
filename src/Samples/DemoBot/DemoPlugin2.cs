using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Platforms.GoCqHttp.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Resources;

namespace DemoBot;

[PluginIdentifier("487ade0a-3afb-4451-8543-78e63bd2c668")]
public class DemoPlugin2 : BasicPlugin<GoCqMessageContext>
{
    private readonly ILogger<DemoPlugin2> _logger;

    public DemoPlugin2(ILogger<DemoPlugin2> logger, ConfigurationFactory configurationFactory)
    {
        _logger = logger;
        var config = configurationFactory.GetConfiguration<TestConfiguration>();
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

public class TestConfiguration : ConfigurationBase
{
    public string Key1 { get; set; }
}