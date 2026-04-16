using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Discord;
using Discord.Net.Rest;
using Discord.Net.WebSockets;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Platforms.Discord.Messaging;

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

        var proxy = CreateProxy(discordOptions);
        if (proxy != null)
        {
            config.RestClientProvider = DefaultRestClientProvider.Create(true, proxy);
            config.WebSocketProvider = DefaultWebSocketProvider.Create(proxy);
        }

        _client = new DiscordSocketClient(config);

        _client.Log += LogAsync;
        _client.MessageReceived += Client_MessageReceived;
        _client.ChannelCreated += Client_ChannelCreated;
        _client.ChannelDestroyed += Client_ChannelDestroyed;
        _client.UserJoined += Client_UserJoined;
        _client.UserLeft += Client_UserLeft;
        _client.GuildMemberUpdated += Client_GuildMemberUpdated;
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

    /// <summary>
    ///     用于在 Dispatcher 中检索联系人增量事件的缓存。
    /// </summary>
    public ConcurrentDictionary<string, DiscordContactEvent> ContactEventCache { get; } = new();

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

        await PublishCachedMessageAsync(guid => MessageCache.TryAdd(guid, arg),
            guid => MessageCache.TryRemove(guid, out _),
            "message");
    }

    private Task Client_ChannelCreated(SocketChannel channel)
    {
        if (channel is not SocketGuildChannel guildChannel || channel is not ITextChannel)
            return Task.CompletedTask;

        var evt = new DiscordChannelCreated(guildChannel.Guild.Id.ToString(), guildChannel.Id.ToString(),
            guildChannel.Name);
        return PublishCachedMessageAsync(guid => ContactEventCache.TryAdd(guid, evt),
            guid => ContactEventCache.TryRemove(guid, out _),
            "channel-created");
    }

    private Task Client_ChannelDestroyed(SocketChannel channel)
    {
        if (channel is not SocketGuildChannel guildChannel || channel is not ITextChannel)
            return Task.CompletedTask;

        var evt = new DiscordChannelRemoved(guildChannel.Guild.Id.ToString(), guildChannel.Id.ToString());
        return PublishCachedMessageAsync(guid => ContactEventCache.TryAdd(guid, evt),
            guid => ContactEventCache.TryRemove(guid, out _),
            "channel-removed");
    }

    private Task Client_UserJoined(SocketGuildUser user)
    {
        if (user.IsBot)
            return Task.CompletedTask;

        var evt = new DiscordMemberJoined(user.Guild.Id.ToString(), user.Id.ToString(), user.Nickname,
            GetMemberRole(user));
        return PublishCachedMessageAsync(guid => ContactEventCache.TryAdd(guid, evt),
            guid => ContactEventCache.TryRemove(guid, out _),
            "member-joined");
    }

    private Task Client_UserLeft(SocketGuild guild, SocketUser user)
    {
        if (user.IsBot)
            return Task.CompletedTask;

        var evt = new DiscordMemberLeft(guild.Id.ToString(), user.Id.ToString());
        return PublishCachedMessageAsync(guid => ContactEventCache.TryAdd(guid, evt),
            guid => ContactEventCache.TryRemove(guid, out _),
            "member-left");
    }

    private Task Client_GuildMemberUpdated(Cacheable<SocketGuildUser, ulong> oldUser, SocketGuildUser newUser)
    {
        if (newUser.IsBot)
            return Task.CompletedTask;

        var evt = new DiscordMemberUpdated(newUser.Guild.Id.ToString(), newUser.Id.ToString(), newUser.Nickname,
            GetMemberRole(newUser));
        return PublishCachedMessageAsync(guid => ContactEventCache.TryAdd(guid, evt),
            guid => ContactEventCache.TryRemove(guid, out _),
            "member-updated");
    }

    private async Task PublishCachedMessageAsync(Func<string, bool> add,
        Action<string> remove,
        string eventType)
    {
        var guid = Guid.NewGuid().ToString();
        if (!add(guid))
            return;

        if (RawMessageReceived != null)
        {
            await RawMessageReceived.Invoke(guid);
        }

        _ = Task.Delay(TimeSpan.FromMinutes(1)).ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogWarning(t.Exception, "Error occurred while cleaning up {EventType} cache for guid: {Guid}",
                    eventType, guid);
                return;
            }

            remove(guid);
        });
    }

    private static MemberRole GetMemberRole(SocketGuildUser user)
    {
        if (user.Guild.OwnerId == user.Id)
            return MemberRole.Owner;
        if (user.Roles.Any(r => r.Permissions.Administrator))
            return MemberRole.Admin;
        return MemberRole.Member;
    }

    private static IWebProxy? CreateProxy(DiscordBotOptions options)
    {
        var proxy = options.Proxy;
        var proxyUrl = string.IsNullOrWhiteSpace(proxy.Url)
            ? options.HttpOptions.ProxyUrl
            : proxy.Url;

        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            if (!Uri.TryCreate(proxyUrl, UriKind.Absolute, out var proxyUri))
            {
                throw new InvalidOperationException($"Invalid Discord proxy url: {proxyUrl}");
            }

            return new WebProxy(proxyUri)
            {
                Credentials = CredentialCache.DefaultCredentials
            };
        }

        if (!proxy.Enabled || !proxy.UseSystemProxy)
        {
            return null;
        }

        var systemProxy = WebRequest.GetSystemWebProxy();
        systemProxy.Credentials = CredentialCache.DefaultCredentials;
        return systemProxy;
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