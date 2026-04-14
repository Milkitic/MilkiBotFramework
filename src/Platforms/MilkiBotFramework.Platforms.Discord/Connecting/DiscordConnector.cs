using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Platforms.Discord.Connecting;

public class DiscordConnector : IConnector
{
    private readonly DiscordBotOptions _options;
    private readonly ILogger<DiscordConnector> _logger;
    private readonly DiscordSocketClient _client;

    // 用于在 Dispatcher 中检索原始消息
    public static readonly ConcurrentDictionary<string, SocketMessage> MessageCache = new();

    public event Func<string, Task>? RawMessageReceived;

    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public TimeSpan ErrorReconnectTimeout { get; set; }
    public TimeSpan MessageTimeout { get; set; }
    public System.Text.Encoding? Encoding { get; set; }

    public DiscordConnector(BotOptions options, ILogger<DiscordConnector> logger)
    {
        if (options is not DiscordBotOptions discordOptions)
        {
            throw new ArgumentException("Options must be of type DiscordBotOptions", nameof(options));
        }

        _options = discordOptions;
        _logger = logger;

        var config = new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent
        };
        _client = new DiscordSocketClient(config);

        _client.Log += LogAsync;
        _client.MessageReceived += Client_MessageReceived;
        _client.Ready += () =>
        {
            _logger.LogInformation("Discord Bot is Ready!");
            return Task.CompletedTask;
        };
    }

    public DiscordSocketClient Client => _client;

    private async Task Client_MessageReceived(SocketMessage arg)
    {
        if (arg.Author.IsBot) return;

        var guid = Guid.NewGuid().ToString();
        MessageCache.TryAdd(guid, arg);

        // 传递 Guid 给 Dispatcher
        if (RawMessageReceived != null)
        {
            await RawMessageReceived.Invoke(guid);
        }

        // 清理 Cache (简单起见，这里不做复杂清理，假设 Dispatcher 会处理或者定期清理)
        // 实际生产中应该有一个过期策略
        _ = Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(_ => { MessageCache.TryRemove(guid, out SocketMessage _); });
    }

    private Task LogAsync(LogMessage arg)
    {
        switch (arg.Severity)
        {
            case LogSeverity.Critical:
                _logger.LogCritical(arg.Exception, arg.Message);
                break;
            case LogSeverity.Error:
                _logger.LogError(arg.Exception, arg.Message);
                break;
            case LogSeverity.Warning:
                _logger.LogWarning(arg.Exception, arg.Message);
                break;
            case LogSeverity.Info:
                _logger.LogInformation(arg.Exception, arg.Message);
                break;
            case LogSeverity.Verbose:
            case LogSeverity.Debug:
                _logger.LogDebug(arg.Exception, arg.Message);
                break;
        }

        return Task.CompletedTask;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await _client.LoginAsync(TokenType.Bot, _options.Token);
        await _client.StartAsync();
    }

    public async Task DisconnectAsync()
    {
        await _client.StopAsync();
        await _client.LogoutAsync();
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        // 这个方法通常用于底层 WebSocket 发送字符串，对于 Discord SDK 并不适用
        // 我们在 MessageApi 中实现具体的发送逻辑
        throw new NotSupportedException("Use MessageApi to send messages.");
    }
}