using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MilkiBotFramework.GoCqHttp.Messaging;
using MilkiBotFramework.Plugins;

namespace DemoBot
{
    [PluginIdentifier("1e1e623a-d89d-49ad-b801-f93dd94cf2d7")]
    public class DemoPlugin : BasicPlugin
    {
    }

    [PluginIdentifier("487ade0a-3afb-4451-8543-78e63bd2c668")]
    public class DemoPlugin2 : BasicPlugin<GoCqMessageRequestContext>
    {
    }
}
