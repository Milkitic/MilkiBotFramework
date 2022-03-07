using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Configuration;

namespace DemoBot;

[PluginIdentifier("1e1e623a-d89d-49ad-b801-f93dd94cf2d7", Index = 1)]
public class DemoPlugin : BasicPlugin
{
    private readonly MyPluginDbContext _myPluginDbContext;
    private readonly DemoPlugin2 _demoPlugin2;
    private readonly ILogger<DemoPlugin> _logger;
    private readonly IConfiguration<TestConfiguration> _configuration;
    private readonly IRichMessageConverter _richMessageConverter;
    private readonly PluginManager _pluginManager;

    public DemoPlugin(MyPluginDbContext myPluginDbContext, DemoPlugin2 demoPlugin2, ILogger<DemoPlugin> logger,
        IConfiguration<TestConfiguration> configuration, IRichMessageConverter richMessageConverter, PluginManager pluginManager)
    {
        _myPluginDbContext = myPluginDbContext;
        _demoPlugin2 = demoPlugin2;
        _logger = logger;
        _configuration = configuration;
        _richMessageConverter = richMessageConverter;
        _pluginManager = pluginManager;
    }

    [CommandHandler("key2")]
    public async Task<IResponse> Key2()
    {
        var result = _configuration.Instance.Key2++;
        await _configuration.Instance.SaveAsync();
        return Reply(result.ToString());
    }

    [CommandHandler("group", AllowedMessageType = MessageType.Channel)]
    public IResponse Group([Argument] string content = "world")
    {
        return Reply("hello group");
    }

    [CommandHandler("private", AllowedMessageType = MessageType.Private)]
    public IResponse Private([Argument] string content = "world")
    {
        return Reply("hello private");
    }

    [CommandHandler("hello")]
    public IResponse EchoRoot1([Argument(Authority = MessageAuthority.Admin)] string content = "world")
    {
        return Reply("hello " + content);
    }

    public override async Task<IResponse?> OnBindingFailed(BindingException bindingException, MessageContext context)
    {
        if (bindingException.BindingSource.CommandInfo.Command == "hello")
        {
            return Reply("hello的参数只有管理员才能使用哦");
        }

        return null;
    }

    [CommandHandler(Authority = MessageAuthority.Admin)]
    public IResponse EchoRoot([Argument] string content) => Reply(content);

    [CommandHandler]
    public IResponse Echo([Argument] string content) => Reply(content);

    [CommandHandler("model")]
    [Description("Echo all of your contents.")]
    public async Task<IResponse> ModelBinding(BindingModel bindingModel)
    {
        return Reply(JsonSerializer.Serialize(new
        {
            bindingModel.Name,
            bindingModel.Age,
            Description = bindingModel.Description.ToString()
        }));
    }

    [CommandHandler("option")]
    [Description("Echo all of your contents.")]
    public IResponse Option([Option("o")] byte option)
    {
        return Reply(((byte)(option + 1)).ToString());
    }

    [CommandHandler("arg")]
    [Description("Echo all of your contents.")]
    public async IAsyncEnumerable<IResponse> Arguments(
        [Argument] ReadOnlyMemory<char> arguments,
        [Argument] MessageAuthority messageAuthority = MessageAuthority.Unspecified)
    {
        yield return Reply(arguments + " " + messageAuthority);
    }

    public override async IAsyncEnumerable<IResponse> OnMessageReceived(MessageContext context)
    {
        var message = context.TextMessage;
        var richMessage = context.GetRichMessage();

        yield return Reply("Not this one! Wait for ur next message!", out var nextMessage);
        var richMsg = (await nextMessage.GetNextMessageAsync(5)).GetRichMessage();
        yield return Reply("OK! your first message is: " + richMsg);
        await Task.Delay(1000);
        yield return Reply(" Wait for ur next message!", out nextMessage);
        richMsg = (await nextMessage.GetNextMessageAsync(5)).GetRichMessage();
        yield return Reply("OK! your second message is: " + richMsg);
    }

    //protected override async Task OnInitialized() => _logger.LogDebug(nameof(OnInitialized));
    //protected override async Task OnUninitialized() => _logger.LogDebug(nameof(OnUninitialized));
    //protected override async Task OnExecuting() => _logger.LogDebug(nameof(OnExecuting));
    //protected override async Task OnExecuted() => _logger.LogDebug(nameof(OnExecuted));
}

public class BindingModel
{
    [Option("name")]
    public string Name { get; set; }
    [Option("age", DefaultValue = 14)]
    public int Age { get; set; }
    [Argument(DefaultValue = "no description")]
    public ReadOnlyMemory<char> Description { get; set; }
}