using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Aspnetcore
{
    public class AspnetcoreBotBuilder : BotBuilderBase<Bot, AspnetcoreBotBuilder>
    {
        private readonly string[] _bindUrls;
        private readonly WebApplicationBuilder _builder;
        private WebApplication? _app;
        private static readonly string[] DefaultUris = { "http://0.0.0.0:5000", "https://0.0.0.0:5001" };

        public AspnetcoreBotBuilder(string[] args, params string[] bindUrls)
        {
            _bindUrls = bindUrls.Length == 0 ? DefaultUris : bindUrls;
#if DEBUG
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
#endif
            _builder = WebApplication.CreateBuilder(args);
        }

        protected override void ConfigServices(IServiceCollection serviceCollection)
        {
            _builder.WebHost.UseUrls(_bindUrls);
            _builder.Services.AddControllers()
                 //.AddApplicationPart(Assembly.GetExecutingAssembly()) // 如果用此方法请注意对应的插件程序集将无法Unload，需重启生效
                 //.AddControllersAsServices()
                 ;
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
                var aspnetcoreConnector = (AspnetcoreConnector)connector;
                var webSocketOptions = new WebSocketOptions
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(2)
                };
                _app.UseWebSockets(webSocketOptions);
                _app.Use(async (context, next) =>
                {
                    var path = connector.BindingPath;
                    if (context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase))
                    {
                        if (context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
                        {
                            if (context.WebSockets.IsWebSocketRequest)
                            {
                                var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                                await aspnetcoreConnector.OnWebSocketOpen(webSocket);
                            }
                            else
                            {
                                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            }
                        }
                        else
                        {
                            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                        }

                    }
                    else
                    {
                        await next(context);
                    }
                });
                //_app.UseMiddleware<ReverseWebsocketMiddleware>();
            }
            else if (connector!.ConnectionType == ConnectionType.Http)
            {
                _app.UseMiddleware<HttpMiddleware>();
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
}