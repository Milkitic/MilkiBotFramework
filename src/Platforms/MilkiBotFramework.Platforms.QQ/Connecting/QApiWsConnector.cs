﻿using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Platforms.QQ.ContactsManaging;
using Websocket.Client;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

/// <summary>旧版 QQ WebSocket 网关连接器，保留以兼容已有集成。</summary>
[Obsolete("推荐使用 UseQQ 注册的 QQ Webhook 连接器。")]
public class QApiWsConnector : WebSocketClientConnector
{
    private readonly LightHttpClient _httpClient;
    private readonly QContactsManager? _contactsManager;
    private readonly ILogger<QApiWsConnector> _logger;

    private DateTimeOffset _tokenExpireTime;
    private string? _accessToken;

    private string? _wsUrl;
    private TimeSpan _heartBeatInterval = Timeout.InfiniteTimeSpan;
    private int _lastSequence;
    private string? _lastSessionId;

    private CancellationTokenSource? _cts;

    private bool _isDropped;

    /// <summary>创建旧版 QQ 网关连接器。</summary>
    /// <param name="httpClient">用于查询网关和访问凭证的 HTTP 客户端。</param>
    /// <param name="contactsManager">用于保存机器人自身信息的联系人管理器。</param>
    /// <param name="logger">网关连接日志。</param>
    public QApiWsConnector(LightHttpClient httpClient, IContactsManager contactsManager, ILogger<QApiWsConnector> logger) : base(logger)
    {
        _httpClient = httpClient;
        _contactsManager = contactsManager as QContactsManager;
        _logger = logger;
        MessageReceived += QApiConnector_MessageReceived;
    }

    /// <summary>当前分配给 QQ 网关的鉴权值。</summary>
    public string Authorization => $"QQBot {_accessToken}";

    /// <summary>最近收到的非空网关事件序号。</summary>
    public int MessageSequence => Volatile.Read(ref _lastSequence);

    private Task QApiConnector_MessageReceived(InboundMessage inboundMessage)
    {
        var message = inboundMessage.RawText;
        if (string.IsNullOrWhiteSpace(message))
            return Task.CompletedTask;

        using var jsonDocument = JsonDocument.Parse(message);
        var rootElement = jsonDocument.RootElement;
        var opProp = rootElement.GetProperty("op");
        var opCode = (OpCode)opProp.GetInt32();
        if (opCode == OpCode.Hello)
        {
            var funcObj = rootElement.GetProperty("d");
            var hbInterval = funcObj.GetProperty("heartbeat_interval").GetInt32();
            var heartBeatInterval = TimeSpan.FromMilliseconds(hbInterval);
            if (_heartBeatInterval != heartBeatInterval)
            {
                _logger.LogDebug($"[QAPI] Update HeartBeat interval: {_heartBeatInterval}->{heartBeatInterval}");
                _heartBeatInterval = heartBeatInterval;
            }
        }
        else if (opCode == OpCode.Dispatch && rootElement.GetProperty("t").GetString() == "READY")
        {
            if (rootElement.GetProperty("d").TryGetProperty("session_id", out var sessionProp))
            {
                _lastSessionId = sessionProp.GetString();
            }
        }

        if (rootElement.TryGetProperty("s", out var sProp) &&
            sProp.ValueKind == JsonValueKind.Number && sProp.TryGetInt32(out var sequence))
        {
            Volatile.Write(ref _lastSequence, sequence);
        }

        return Task.CompletedTask;
    }

    /// <summary>旧版网关的机器人接入配置。</summary>
    public QConnection? Connection { get; internal set; }

    /// <summary>查询 WebSocket 入口时使用的 QQ OpenAPI 主机名。</summary>
    public string Host
    {
        get
        {
            if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));
            return QApiTokenHelper.GetHost(Connection);
        }
    }

    /// <summary>请求 QQ 网关地址并连接 WebSocket。</summary>
    /// <param name="cancellationToken">取消连接的令牌。</param>
    public override async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));
        await UpdateAccessTokenAsync(cancellationToken);
        await RequestEndpointAsync(cancellationToken);

        TargetUri = _wsUrl;

        await base.ConnectAsync(cancellationToken);
    }

    /// <summary>在网关重连后恢复或重新建立会话，然后启动心跳。</summary>
    /// <param name="reconnectionInfo">网关库报告的重连原因。</param>
    protected override async ValueTask OnReconnectionHappened(ReconnectionInfo reconnectionInfo)
    {
        if (reconnectionInfo.Type != ReconnectionType.Initial)
            _logger.LogError("OnReconnectionHappened: " + reconnectionInfo.Type);
        await StopHeartbeatAsync();
        if (DateTimeOffset.UtcNow >= _tokenExpireTime)
        {
            await UpdateAccessTokenAsync();
        }

        var resumed = _isDropped && _lastSessionId is { } sessionId && await TryResumeSessionAsync(sessionId) == true;
        if (resumed || await TryIdentifyAsync() == true)
        {
            var heartbeat = new CancellationTokenSource();
            _cts = heartbeat;
            _ = KeepSendHeartbeat(heartbeat.Token);
        }
        else
        {
            await Task.Delay(ErrorReconnectTimeout);
            await Client!.Reconnect();
        }
    }

    /// <summary>停止当前网关会话的心跳，并记录是否可尝试恢复会话。</summary>
    /// <param name="disconnectionInfo">网关库报告的断开原因。</param>
    protected override async ValueTask OnDisconnectionHappened(DisconnectionInfo disconnectionInfo)
    {
        _logger.LogError(disconnectionInfo.Exception, "OnDisconnectionHappened");
        _isDropped = disconnectionInfo.Type != DisconnectionType.ByServer;
        await StopHeartbeatAsync();
    }

    private async Task StopHeartbeatAsync()
    {
        using var heartbeat = Interlocked.Exchange(ref _cts, null);
        if (heartbeat != null)
        {
            await heartbeat.CancelAsync();
        }
    }

    private async Task UpdateAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var (accessToken, expireTime) = await QApiTokenHelper.RequestAccessTokenAsync(_httpClient, Connection!, cancellationToken);
        _accessToken = accessToken;
        _tokenExpireTime = expireTime;
    }

    private async Task RequestEndpointAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.SendOnceAsync(HttpMethod.Get, new Uri($"https://{Host}/gateway"),
            headers: new Dictionary<string, string>
            {
                ["Authorization"] = Authorization
            }, cancellationToken: cancellationToken);
        var result = await QOpenApiResponse.ReadAsync(response, cancellationToken) as JsonObject;
        if (!Uri.TryCreate(result?["url"]?.ToString(), UriKind.Absolute, out var gateway) || gateway.Scheme != "wss")
        {
            throw new QApiException("QQ 网关响应缺少有效的 WSS 地址", null, response.StatusCode);
        }

        _wsUrl = gateway.AbsoluteUri;
    }

    private async Task<bool?> TryIdentifyAsync()
    {
        var intents = Intents.Guilds | Intents.GuildMembers | Intents.GuildMessageReactions |
                      Intents.DirectMessage | Intents.GroupAndC2CEvent | Intents.Interaction |
                      Intents.MessageAudit | Intents.AudioAction | Intents.PublicGuildMessages;
        var verify = JsonSerializer.Serialize(new
        {
            op = 2,
            // ReSharper disable RedundantAnonymousTypePropertyName
            d = new
            {
                token = Authorization,
                intents = intents,
                shard = new[] { 0, 1 },
            }
            // ReSharper restore RedundantAnonymousTypePropertyName
        });
        using var filter = SendMessage(verify);
        _logger.LogDebug($"[QAPI] Verifying bot with {intents}");
        var identifyResult = await filter.FilterMessageAsync<bool?>(asyncWsMessage =>
        {
            using var jsonDocument = JsonDocument.Parse(asyncWsMessage.Message);
            var rootElement = jsonDocument.RootElement;
            var opProp = rootElement.GetProperty("op");
            var opCode = (OpCode)opProp.GetInt32();
            if (opCode == OpCode.Dispatch)
            {
                var t = rootElement.GetProperty("t").GetString();
                var result = t == "READY";
                if (!result)
                {
                    return null;
                }
                else
                {
                    asyncWsMessage.IsHandled = true;
                    var dProp = rootElement.GetProperty("d").GetProperty("user");
                    var id = dProp.GetProperty("id").GetString()!;
                    var username = dProp.GetProperty("username").GetString();
                    _contactsManager?.UpdateSelfInfo(new SelfInfo
                    {
                        UserId = id,
                        Nickname = username
                    });
                    _logger.LogDebug($"[QAPI] Verification passed");
                }

                return result;
            }

            if (opCode == OpCode.InvalidSession)
            {
                asyncWsMessage.IsHandled = true;
                _logger.LogError($"[QAPI] {opCode}");
                return false;
            }

            _logger.LogWarning($"[QAPI] {opCode}");
            return null;
        });

        return identifyResult;
    }

    private async Task<bool?> TryResumeSessionAsync(string sessionId)
    {
        var resume = JsonSerializer.Serialize(new
        {
            op = 6,
            d = new
            {
                token = Authorization,
                session_id = sessionId,
                seq = MessageSequence
            }
        });

        using var filter = SendMessage(resume);
        _logger.LogDebug($"[QAPI] Trying resume session");
        var resumeStatus = await filter.FilterMessageAsync<bool?>(asyncWsMessage =>
        {
            using var jsonDocument = JsonDocument.Parse(asyncWsMessage.Message);
            var rootElement = jsonDocument.RootElement;
            var opProp = rootElement.GetProperty("op");
            var opCode = (OpCode)opProp.GetInt32();
            if (opCode == OpCode.Dispatch)
            {
                var t = rootElement.GetProperty("t").GetString();
                var result = t == "RESUMED";
                if (!result)
                {
                    return null;
                }
                else
                {
                    asyncWsMessage.IsHandled = true;
                    _logger.LogDebug("[QAPI] Session resumed");
                    return true;
                }
            }
            else if (opCode == OpCode.InvalidSession)
            {
                asyncWsMessage.IsHandled = true;
                _logger.LogWarning($"[QAPI] {opCode}");
                return false;
            }

            _logger.LogWarning($"[QAPI] {opCode}");
            return null;
        });

        return resumeStatus;
    }

    private async Task KeepSendHeartbeat(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var sequence = MessageSequence;
                Client!.Send(JsonSerializer.Serialize(new
                {
                    op = (int)OpCode.Heartbeat,
                    d = sequence == 0 ? (int?)null : sequence
                }));
                await Task.Delay(_heartBeatInterval, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // 当前网关会话已关闭。
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "QQ 网关心跳发送失败。");
        }
    }
}
