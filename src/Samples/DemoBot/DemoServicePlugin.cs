﻿using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Loading;

namespace DemoBot;

[PluginIdentifier("850cea3e-b448-4f45-a2c7-c6bc708ccb3f")]
[Author("Milkitic")]
[Version("1.0.0")]
[Description("Demo service plugin description here!")]
public class DemoServicePlugin : ServicePlugin
{
    private readonly ILogger<DemoServicePlugin> _logger;
    private readonly IMessageApi _messageApi;
    private readonly DemoPlugin _demoPlugin;
    private readonly PluginManager _pluginManager;

    public DemoServicePlugin(ILogger<DemoServicePlugin> logger,
        IMessageApi messageApi,
        DemoPlugin demoPlugin,
        PluginManager pluginManager)
    {
        _logger = logger;
        _messageApi = messageApi;
        _demoPlugin = demoPlugin;
        _pluginManager = pluginManager;
    }

    protected override async Task OnInitialized()
    {
        _logger.LogInformation(JsonSerializer.Serialize(Metadata));
    }
}