using Microsoft.AspNetCore.Builder;
using MilkiBotFramework.Aspnetcore;
using MilkiBotFramework.Platforms.QQ.Connecting;

namespace MilkiBotFramework.Platforms.QQ;

public class QBotBuilder : AspnetcoreBotBuilder
{
    protected override void ConfigureMiddleware(IServiceProvider serviceProvider)
    {
        WebApp.UseMiddleware<QApiHttpMiddleware>();
    }
}