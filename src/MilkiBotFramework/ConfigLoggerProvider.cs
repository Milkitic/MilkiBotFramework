using System;
using Microsoft.Extensions.Logging;

namespace MilkiBotFramework;

public class ConfigLoggerProvider
{
    public Action<ILoggingBuilder> ConfigureLogger { get; }

    public ConfigLoggerProvider(Action<ILoggingBuilder> configureLogger)
    {
        ConfigureLogger = configureLogger;
    }
}