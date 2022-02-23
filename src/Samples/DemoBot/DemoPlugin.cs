using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;

namespace DemoBot
{
    [PluginIdentifier("1e1e623a-d89d-49ad-b801-f93dd94cf2d7")]
    public class DemoPlugin : BasicPlugin
    {
        private readonly DemoPlugin2 _demoPlugin2;
        private readonly ILogger<DemoPlugin> _logger;

        public DemoPlugin(DemoPlugin2 demoPlugin2, ILogger<DemoPlugin> logger)
        {
            _demoPlugin2 = demoPlugin2;
            _logger = logger;
        }

        [CommandHandler("asdf")]
        [Description("Echo all of your contents.")]
        public async Task Haha(MessageContext context, [Argument] string arguments)
        {
            await context.Response.QuickReply(arguments);
        }
         
        public override async Task OnMessageReceived(MessageContext context)
        {
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
}
