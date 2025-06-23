using Microsoft.AspNetCore.Builder;
using MilkiBotFramework.Aspnetcore;

namespace MilkiBotFramework.Platforms.QQ;

public class QBotBuilder : AspnetcoreBotBuilder
{
    protected override void ConfigureMiddleware(IServiceProvider serviceProvider)
    {
        WebApp.UseMiddleware<QApiHttpMiddleware>();
    }
}