using System.Reflection;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Aspnetcore
{
    public class AspnetcoreBotBuilder : BotBuilderBase<AspnetcoreBot, AspnetcoreBotBuilder>
    {
        private readonly WebApplicationBuilder _builder;
        private WebApplication? _app;

        public AspnetcoreBotBuilder(string[] args)
        {
#if DEBUG
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
#endif
            _builder = WebApplication.CreateBuilder(args);
        }

        protected override void ConfigServices(IServiceCollection serviceCollection)
        {
            _builder.WebHost.UseUrls("http://0.0.0.0:23333");
            // 如果用此方法请注意对应的插件程序集将无法Unload，需重启生效
            _builder.Services.AddControllers()
                .AddApplicationPart(Assembly.GetExecutingAssembly())
                .AddControllersAsServices();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            _builder.Services.AddEndpointsApiExplorer();
            //_builder.Services.AddSwaggerGen();
            base.ConfigServices(serviceCollection);
        }

        protected override IServiceProvider BuildCore(IServiceCollection services)
        {
            services.AddSingleton(typeof(WebApplication), _ => _app!);
            _app = _builder.Build();
            return _app.Services;
        }

        protected override void ConfigureApp(IServiceProvider serviceProvider)
        {
            base.ConfigureApp(serviceProvider);

            if (_app == null) return;
            //if (_app.Environment.IsDevelopment())
            //{
            //    _app.UseSwagger();
            //    _app.UseSwaggerUI();
            //}

            var connector = serviceProvider.GetService<IConnector>();
            if (connector!.ConnectionType == ConnectionType.ReverseWebsocket)
            {
                _app.UseMiddleware<WsMiddleware>();
                var webSocketOptions = new WebSocketOptions
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(2)
                };
                _app.UseWebSockets(webSocketOptions);
            }

            _app.UseHttpsRedirection();
            _app.UseAuthorization();

            _app.MapControllers();
        }

        protected override IServiceCollection GetServiceCollection()
        {
            return _builder.Services;
        }
    }

    public class WsMiddleware
    {
        private readonly RequestDelegate _next;

        public WsMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // dynamic invoke
        public async Task InvokeAsync(HttpContext context, IServiceProvider services)
        {
            if (context.Request.Path.Equals("/connector/ws", StringComparison.OrdinalIgnoreCase))
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            }
            else
            {
                await _next(context);
            }
        }
    }
}