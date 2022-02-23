using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;

namespace DemoBot
{
    [PluginIdentifier("1e1e623a-d89d-49ad-b801-f93dd94cf2d7")]
    public class DemoPlugin : BasicPlugin
    {
        [CommandHandler("echo")]
        [Description("Echo all of your contents.")]
        public async Task Haha(MessageContext messageContext, [Argument] string arguments)
        {

        }
    }
}
