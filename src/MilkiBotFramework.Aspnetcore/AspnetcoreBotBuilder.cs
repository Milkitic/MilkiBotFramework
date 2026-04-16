using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Aspnetcore
{
    public class AspnetcoreBotBuilder : BotBuilderBase<Bot, AspnetcoreBotBuilder>
    {
        public static readonly string[] DefaultUris = { "http://0.0.0.0:5000", "https://0.0.0.0:5001" };

        private readonly WebApplicationBuilder _builder;

        public AspnetcoreBotBuilder(params string[] bindUrls)
        {
            BindUrls = bindUrls.Length == 0 ? DefaultUris : bindUrls;
#if DEBUG
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
#endif
            _builder = WebApplication.CreateBuilder();
            _builder.Logging.ClearProviders();
            _builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        }

        public AspnetcoreBotBuilder(string[] args, params string[] bindUrls)
        {
            BindUrls = bindUrls.Length == 0 ? DefaultUris : bindUrls;
#if DEBUG
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
#endif
            _builder = WebApplication.CreateBuilder(args);
            _builder.Logging.ClearProviders();
            _builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());
        }

        public WebApplication WebApp { get; private set; } = null!;
        public string[] BindUrls { get; private set; }

        public AspnetcoreBotBuilder UseUrl(params string[] bindUrls)
        {
            BindUrls = bindUrls.Length == 0 ? DefaultUris : bindUrls;
            return this;
        }

        protected override void ConfigServices(IServiceCollection serviceCollection)
        {
            ConfigureBuilder(_builder);
            base.ConfigServices(serviceCollection);
        }

        protected virtual void ConfigureBuilder(WebApplicationBuilder builder)
        {
            _builder.WebHost.UseUrls(BindUrls);
            var mvcBuilder = _builder.Services.AddControllers()
                //.AddApplicationPart(Assembly.GetExecutingAssembly()) // 如果用此方法请注意对应的插件程序集将无法Unload，需重启生效
                //.AddControllersAsServices()
                ;
            ConfigureMvc(mvcBuilder);
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            _builder.Services.AddEndpointsApiExplorer();
            //_builder.Services.AddSwaggerGen();
        }

        protected virtual void ConfigureMvc(IMvcBuilder mvcBuilder)
        {

        }

        protected override IServiceProvider BuildCore(IServiceCollection services)
        {
            services.AddSingleton(typeof(WebApplication), _ => WebApp);
            WebApp = _builder.Build();
            return WebApp.Services;
        }

        protected override void ConfigureApp(IServiceProvider serviceProvider)
        {
            base.ConfigureApp(serviceProvider);
            //if (_app.Environment.IsDevelopment())
            //{
            //    _app.UseSwagger();
            //    _app.UseSwaggerUI();
            //}

            ConfigureMiddleware(serviceProvider);

            //_app.UseHttpsRedirection();
            WebApp.UseAuthorization();

            WebApp.MapControllers();
        }

        protected virtual void ConfigureMiddleware(IServiceProvider serviceProvider)
        {
            var connector = serviceProvider.GetService<IConnector>()!;
            if (connector.ConnectionType == ConnectionType.ReverseWebSocket)
            {
                var webSocketOptions = new WebSocketOptions
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(2)
                };
                WebApp.UseWebSockets(webSocketOptions);
                WebApp.UseMiddleware<ReverseWebSocketMiddleware>();
            }
            else if (connector.ConnectionType == ConnectionType.Http)
            {
                WebApp.UseMiddleware<HttpMiddleware>();
            }
        }

        protected override IServiceCollection GetServiceCollection()
        {
            return _builder.Services;
        }
    }
}