using System.Collections.Concurrent;
using System.Text;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Platforms.Discord.Connecting;

public class DiscordConnector : IConnector
{
    private readonly DiscordSocketClient _client;
    private readonly ILogger<DiscordConnector> _logger;
    private readonly DiscordBotOptions _options;

    public DiscordConnector(BotOptions options, ILogger<DiscordConnector> logger)
    {
        if (options is not DiscordBotOptions discordOptions)
        {
            throw new ArgumentException("Options must be of type DiscordBotOptions", nameof(options));
        }

        _options = discordOptions;
        _logger = logger;

        var gatewayIntents = discordOptions.GatewayIntents
                             ?? (GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent);

        var config = new DiscordSocketConfig
        {
            GatewayIntents = gatewayIntents
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

    /// <summary>
    ///     用于在 Dispatcher 中检索原始消息的缓存。
    ///     <para>改为实例字段，避免多 Bot 实例场景下的静态共享冲突。</para>
    /// </summary>
    public ConcurrentDictionary<string, SocketMessage> MessageCache { get; } = new();

    public event Func<string, Task>? RawMessageReceived;

    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public TimeSpan ErrorReconnectTimeout { get; set; }
    public TimeSpan MessageTimeout { get; set; }
    public Encoding? Encoding { get; set; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        // Discord.Net 的 LoginAsync/StartAsync 不直接支持 CancellationToken，
        // 使用 Task.WhenAny 实现超时取消
        var connectTask = Task.Run(async () =>
        {
            await _client.LoginAsync(TokenType.Bot, _options.Token);
            await _client.StartAsync();
        }, cancellationToken);

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
        var completedTask = await Task.WhenAny(connectTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            _logger.LogWarning("Discord connection timed out, attempting to continue...");
        }
        else
        {
            await connectTask; // 传播可能的异常
        }
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

    private async Task Client_MessageReceived(SocketMessage arg)
    {
        // 只过滤自身 Bot 的消息，不过滤其他 Bot 的消息
        if (arg.Author.Id == _client.CurrentUser.Id) return;

        var guid = Guid.NewGuid().ToString();
        MessageCache.TryAdd(guid, arg);

        // 传递 Guid 给 Dispatcher
        if (RawMessageReceived != null)
        {
            await RawMessageReceived.Invoke(guid);
        }

        // 清理 Cache：1 分钟后自动移除，添加错误处理
        _ = Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogWarning(t.Exception, "Error occurred while cleaning up message cache for guid: {Guid}",
                    guid);
                return;
            }

            MessageCache.TryRemove(guid, out _);
        });
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
}