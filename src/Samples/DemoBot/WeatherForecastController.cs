// ReSharper disable All
#pragma warning disable CS1998
#nullable disable

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Plugining;

namespace DemoBot
{
    //[ApiController]
    //[Route("[controller]")]
    //public class SbController : HomeController
    //{
    //}

    [ApiController]
    [Route("[controller]")]
    public class WeatherForecastController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<WeatherForecastController> _logger;
        private readonly PluginManager _pluginManager;

        public WeatherForecastController(ILogger<WeatherForecastController> logger, PluginManager pluginManager)
        {
            _logger = logger;
            _pluginManager = pluginManager;
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateTime.Now.AddDays(index),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }
    }
}