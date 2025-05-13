﻿using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Aspnetcore
{
    public class AspnetcoreBotBuilder : BotBuilderBase<Bot, AspnetcoreBotBuilder>
    {
        public static readonly string[] DefaultUris = { "http://0.0.0.0:5000", "https://0.0.0.0:5001" };

        private WebApplication? _app;
        private readonly WebApplicationBuilder _builder;

        public AspnetcoreBotBuilder(params string[] bindUrls)
        {
            BindUrls = bindUrls.Length == 0 ? DefaultUris : bindUrls;
#if DEBUG
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
#endif
            _builder = WebApplication.CreateBuilder();
            _builder.Logging.ClearProviders();
        }

        public AspnetcoreBotBuilder(string[] args, params string[] bindUrls)
        {
            BindUrls = bindUrls.Length == 0 ? DefaultUris : bindUrls;
#if DEBUG
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
#endif
            _builder = WebApplication.CreateBuilder(args);
            _builder.Logging.ClearProviders();
        }

        public string[] BindUrls { get; private set; }

        public AspnetcoreBotBuilder UseUrl(params string[] bindUrls)
        {
            BindUrls = bindUrls.Length == 0 ? DefaultUris : bindUrls;
            return this;
        }

        protected override void ConfigServices(IServiceCollection serviceCollection)
        {
            _builder.WebHost.UseUrls(BindUrls);
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

            var connector = serviceProvider.GetService<IConnector>()!;
            if (connector.ConnectionType == ConnectionType.ReverseWebSocket)
            {
                var webSocketOptions = new WebSocketOptions
                {
                    KeepAliveInterval = TimeSpan.FromSeconds(2)
                };
                _app.UseWebSockets(webSocketOptions);
                _app.UseMiddleware<ReverseWebSocketMiddleware>();
            }
            else if (connector.ConnectionType == ConnectionType.Http)
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