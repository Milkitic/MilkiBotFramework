using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.GoCqHttp.Connecting;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Loading;

namespace DemoPlugin;

[PluginIdentifier("e5b9df0a-3954-49e3-a119-aace9af22cfa")]
[Author("test")]
[Version("1.0.0-beta.1")]
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