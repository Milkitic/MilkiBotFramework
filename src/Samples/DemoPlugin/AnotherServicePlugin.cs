// ReSharper disable All
#pragma warning disable CS1998
#nullable disable

using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;

namespace DemoPlugin;

[PluginIdentifier("e5b9df0a-3954-49e3-a119-aace9af22cfa", Authors = "test")]
[Description("asdfasfdsfasdf")]
public class AnotherServicePlugin : ServicePlugin
{
    private readonly ILogger<AnotherServicePlugin> _logger;
    private readonly IMessageApi _messageApi;
    private readonly GoCqApi _goCqApi;
    private readonly PluginManager _pluginManager;

    public AnotherServicePlugin(ILogger<AnotherServicePlugin> logger,
        IMessageApi messageApi,
        GoCqApi goCqApi,
        PluginManager pluginManager)
    {
        _logger = logger;
        _messageApi = messageApi;
        _goCqApi = goCqApi;
        _pluginManager = pluginManager;
    }

    protected override async Task OnInitialized()
    {
        _logger.LogInformation(JsonSerializer.Serialize(Metadata));
    }
}