// ReSharper disable All
#pragma warning disable CS1998
#nullable disable

using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.OneBot.Connecting;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;

namespace DemoPlugin;

[PluginIdentifier("e5b9df0a-3954-49e3-a119-aace9af22cfa", Authors = "test")]
[Description("asdfasfdsfasdf")]
public class AnotherServicePlugin : ServicePlugin
{
    private readonly ILogger<AnotherServicePlugin> _logger;
    private readonly IMessageApi _messageApi;
    private readonly OneBotApi _oneBotApi;

    public AnotherServicePlugin(ILogger<AnotherServicePlugin> logger,
        IMessageApi messageApi,
        OneBotApi oneBotApi)
    {
        _logger = logger;
        _messageApi = messageApi;
        _oneBotApi = oneBotApi;
    }

    protected override async Task OnInitialized()
    {
        _logger.LogInformation(JsonSerializer.Serialize(Metadata));
    }
}