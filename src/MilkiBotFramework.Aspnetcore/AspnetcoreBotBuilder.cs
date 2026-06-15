using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Dispatching;

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
            var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
            var platformConnectors = serviceProvider.GetServices<IPlatformConnector>()
                .OfType<AspnetcoreConnector>()
                .Select(connector => (Connector: connector, Transport: ((IPlatformConnector)connector).PlatformId))
                .ToArray();

            if (platformConnectors.Length == 0 && serviceProvider.GetService<IConnector>() is AspnetcoreConnector singleConnector)
            {
                var transport = singleConnector is IPlatformConnector platformConnector
                    ? platformConnector.PlatformId
                    : "http";
                platformConnectors = [(singleConnector, transport)];
            }

            if (platformConnectors.Any(k => k.Connector.ConnectionType == ConnectionType.ReverseWebSocket))
            {
                var webSocketOptions = new WebSocketOptions
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(2)
                };
                WebApp.UseWebSockets(webSocketOptions);
            }

            foreach (var (connector, transport) in platformConnectors)
            {
                if (connector.ConnectionType == ConnectionType.ReverseWebSocket)
                {
                    WebApp.Use(async (context, next) =>
                    {
                        if (!context.Request.Path.Equals(connector.BindingPath, StringComparison.OrdinalIgnoreCase))
                        {
                            await next(context);
                            return;
                        }

                        if (!context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                        {
                            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                            return;
                        }

                        if (!context.WebSockets.IsWebSocketRequest)
                        {
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            return;
                        }

                        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        await connector.OnWebSocketOpen(webSocket, context.Request.Headers);
                    });
                    continue;
                }

                if (connector.ConnectionType != ConnectionType.Http)
                {
                    continue;
                }

                WebApp.Use(async (context, next) =>
                {
                    if (!context.Request.Path.Equals(connector.BindingPath, StringComparison.OrdinalIgnoreCase))
                    {
                        await next(context);
                        return;
                    }

                    if (!context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                        return;
                    }

                    using var reader = new StreamReader(context.Request.Body, System.Text.Encoding.UTF8, true, 1024, true);
                    var bodyStr = await reader.ReadToEndAsync();
                    await dispatcher.InvokeMessageReceived(InboundMessage.FromRawText(bodyStr, transport));
                });
            }
        }

        protected override IServiceCollection GetServiceCollection()
        {
            return _builder.Services;
        }
    }
}