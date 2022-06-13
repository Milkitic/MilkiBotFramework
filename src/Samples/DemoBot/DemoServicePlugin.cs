// ReSharper disable All
#pragma warning disable CS1998
#nullable disable

using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;

namespace DemoBot;

[PluginIdentifier("850cea3e-b448-4f45-a2c7-c6bc708ccb3f", Authors = "Milkitic")]
[Description("Demo service plugin description here!")]
public class DemoServicePlugin : ServicePlugin
{
    private readonly ILogger<DemoServicePlugin> _logger;
    private readonly IMessageApi _messageApi;
    private readonly DemoPlugin _demoPlugin;
    private readonly PluginManager _pluginManager;
    private readonly MyPluginDbContext _myPluginDbContext;

    public DemoServicePlugin(ILogger<DemoServicePlugin> logger,
        IMessageApi messageApi,
        DemoPlugin demoPlugin,
        PluginManager pluginManager,
        MyPluginDbContext myPluginDbContext)
    {
        _logger = logger;
        _messageApi = messageApi;
        _demoPlugin = demoPlugin;
        _pluginManager = pluginManager;
        _myPluginDbContext = myPluginDbContext;
    }

    protected override async Task OnInitialized()
    {
        _logger.LogInformation(JsonSerializer.Serialize(Metadata));
    }
}