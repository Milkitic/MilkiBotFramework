using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace MilkiBotFramework.Aspnetcore
{
    public class AspnetcoreBotBuilder : BotBuilderBase<AspnetcoreBot, AspnetcoreBotBuilder>
    {
        private readonly WebApplicationBuilder _builder;
        private WebApplication _app;

        public AspnetcoreBotBuilder(string[] args)
        {
            _builder = WebApplication.CreateBuilder(args);
        }

        protected override void ConfigServices(IServiceCollection serviceCollection)
        {
            _builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            _builder.Services.AddEndpointsApiExplorer();

            base.ConfigServices(serviceCollection);
        }

        protected override IServiceProvider BuildCore(IServiceCollection services)
        {
            _app = _builder.Build();
            return _app.Services;
        }

        protected override void ConfigureApp(IServiceProvider serviceProvider)
        {
            base.ConfigureApp(serviceProvider);

            _app.UseHttpsRedirection();
            _app.UseAuthorization();

            _app.MapControllerRoute(
                "connector",
                "Connector/{controller}/{action}/{content?}",
                new { controller = "Home", action = "Index" },
                new { Namespace = "MilkiBotFramework.Aspnetcore.HomeController" } // Namespace
            );
            _app.MapControllers();

            var bot = (AspnetcoreBot)serviceProvider.GetService(typeof(Bot));
            bot.WebApplication = _app;
        }

        protected override IServiceCollection GetServiceCollection()
        {
            return _builder.Services;
        }
    }

    public class NamespaceConstraint : ActionMethodSelectorAttribute
    {
        public override bool IsValidForRequest(RouteContext routeContext, ActionDescriptor action)
        {
            var dataTokenNamespace = (string)routeContext.RouteData.DataTokens.FirstOrDefault(dt => dt.Key == "Namespace").Value;
            var actionNamespace = ((ControllerActionDescriptor)action).MethodInfo.DeclaringType.FullName;

            return dataTokenNamespace == actionNamespace;
        }
    }
}