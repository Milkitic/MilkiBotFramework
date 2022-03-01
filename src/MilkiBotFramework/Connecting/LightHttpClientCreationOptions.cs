using System;

namespace MilkiBotFramework.Connecting;

public sealed class LightHttpClientCreationOptions
{
    public string? ProxyUrl { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(8);
    public int RetryCount { get; set; } = 3;
}