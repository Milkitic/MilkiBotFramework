using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;
using MilkiBotFramework.Plugining.Loading;

namespace DemoBot
{
    [PluginIdentifier("1e1e623a-d89d-49ad-b801-f93dd94cf2d7", Index = 1)]
    public class DemoPlugin : BasicPlugin
    {
        private readonly DemoPlugin2 _demoPlugin2;
        private readonly ILogger<DemoPlugin> _logger;
        private readonly IRichMessageConverter _richMessageConverter;
        private readonly PluginManager _pluginManager;

        public DemoPlugin(DemoPlugin2 demoPlugin2, ILogger<DemoPlugin> logger, IRichMessageConverter richMessageConverter, PluginManager pluginManager)
        {
            _demoPlugin2 = demoPlugin2;
            _logger = logger;
            _richMessageConverter = richMessageConverter;
            _pluginManager = pluginManager;
        }

        [CommandHandler("sign")]
        [Description("Echo all of your contents.")]
        public async Task ModelBinding(MessageContext context, BindingModel bindingModel)
        {
            await context.Response.QuickReply(JsonSerializer.Serialize(new
            {
                bindingModel.Name,
                bindingModel.Age,
                Description = bindingModel.Description.ToString()
            }));
        }

        [CommandHandler("option")]
        [Description("Echo all of your contents.")]
        public async Task Option(MessageContext context, [Option("o")] byte option)
        {
            await context.Response.QuickReply(((byte)(option + 1)).ToString());
        }

        [CommandHandler("arg")]
        [Description("Echo all of your contents.")]
        public async Task Arguments(MessageContext context,
            [Argument] ReadOnlyMemory<char> arguments,
            [Argument] MessageAuthority messageAuthority = MessageAuthority.Unspecified)
        {
            await context.Response.QuickReply(arguments + " " + messageAuthority);
        }

        public override async Task OnMessageReceived(MessageContext context)
        {
            var message = context.Request.TextMessage;
            var richMessage = context.Request.GetRichMessage();

            await context.Response.QuickReply("not this one!");
        }

        protected override async Task OnInitialized()
        {
            _logger.LogDebug(nameof(OnInitialized));
        }

        protected override async Task OnUninitialized()
        {
            _logger.LogDebug(nameof(OnUninitialized));
        }

        protected override async Task OnExecuting()
        {
            _logger.LogDebug(nameof(OnExecuting));
        }

        protected override async Task OnExecuted()
        {
            _logger.LogDebug(nameof(OnExecuted));
        }
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
}
