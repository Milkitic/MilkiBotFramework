﻿using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.ContactsManaging;
using MilkiBotFramework.ContactsManaging.Models;
using MilkiBotFramework.Platforms.QQ.ContactsManaging;
using Websocket.Client;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApiConnector : WebSocketClientConnector
{
    private const string ProductHost = "api.sgroup.qq.com";
    private const string SandboxHost = "sandbox.api.sgroup.qq.com";

    private readonly LightHttpClient _httpClient;
    private readonly QContactsManager? _contactsManager;
    private readonly ILogger<QApiConnector> _logger;

    private DateTime _tokenExpireTime;
    private string? _accessToken;

    private string? _wsUrl;
    private TimeSpan _heartBeatInterval = Timeout.InfiniteTimeSpan;
    private int _lastSequence;
    private Guid _lastSessionId;

    private CancellationTokenSource? _cts;

    private bool _isDropped;

    public QApiConnector(LightHttpClient httpClient, IContactsManager contactsManager, ILogger<QApiConnector> logger) : base(logger)
    {
        _httpClient = httpClient;
        _contactsManager = (QContactsManager?)contactsManager;
        _logger = logger;
        RawMessageReceived += QApiConnector_RawMessageReceived;
    }

    public string Authorization => $"QQBot {_accessToken}";
    public int MessageSequence => _lastSequence;

    private Task QApiConnector_RawMessageReceived(string message)
    {
        var jsonDocument = JsonDocument.Parse(message);
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
        else if (opCode == OpCode.Dispatch)
        {
            if (rootElement.GetProperty("d").TryGetProperty("session_id", out var sessionProp))
            {
                _lastSessionId = Guid.Parse(sessionProp.GetString()!);
            }
        }

        if (rootElement.TryGetProperty("s", out var sProp))
        {
            _lastSequence = sProp.GetInt32();
        }

        return Task.CompletedTask;
    }

    public QConnection? Connection { get; internal set; }

    public string Host
    {
        get
        {
            if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));
            return Connection.IsDevelopment ? SandboxHost : ProductHost;
        }
    }

    public override async Task ConnectAsync()
    {
        if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));
        await RequestAccessTokenAsync(Connection);
        await RequestEndpointAsync();

        TargetUri = _wsUrl;

        //var s = JsonSerializer.Deserialize<resp_getAppAccessToken>(response);
        await base.ConnectAsync();
    }

    protected override async ValueTask OnReconnectionHappened(ReconnectionInfo reconnectionInfo)
    {
        if (DateTime.Now >= _tokenExpireTime)
        {
            await RequestAccessTokenAsync(Connection!);
        }

        bool isResume;
        if (_isDropped && _lastSessionId != default)
        {
            isResume = await TryResumeSessionAsync();
        }
        else
        {
            isResume = false;
        }

        if (isResume || await TryIdentifyAsync() == true)
        {
            _cts = new CancellationTokenSource();
            _ = Task.Factory.StartNew(async () => { await KeepSendHeartbeat(_cts.Token); });
        }
        else
        {
            await Task.Delay(ErrorReconnectTimeout);
            await Client!.Reconnect();
        }
    }

    protected override async ValueTask OnDisconnectionHappened(DisconnectionInfo disconnectionInfo)
    {
        _isDropped = disconnectionInfo.Type != DisconnectionType.ByServer;
        if (_cts == null) return;
        await _cts.CancelAsync();
        _cts.Dispose();
        _cts = null;
    }

    private async Task RequestAccessTokenAsync(QConnection connection)
    {
        var response = await _httpClient.HttpPost<string>(
            "https://bots.qq.com/app/getAppAccessToken",
            new
            {
                appId = connection.AppId,
                clientSecret = connection.ClientSecret
            });

        var jsonNode = JsonNode.Parse(response)!;
        ValidateResult(jsonNode);

        var accessToken = jsonNode["access_token"]!.GetValue<string>();
        var expiresIn = int.Parse(jsonNode["expires_in"]!.GetValue<string>());
        _accessToken = accessToken;
        _tokenExpireTime = DateTime.Now.AddSeconds(expiresIn);
    }

    private async Task RequestEndpointAsync()
    {
        var response2 = await _httpClient.HttpGet<string>(
            $"https://{Host}/gateway", headers: new Dictionary<string, string>()
            {
                ["Authorization"] = Authorization
            });
        var jsonNode2 = JsonNode.Parse(response2)!;
        ValidateResult(jsonNode2);
        _wsUrl = jsonNode2["url"]!.GetValue<string>();
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
            var jsonDocument = JsonDocument.Parse(asyncWsMessage.Message);
            var rootElement = jsonDocument.RootElement;
            var opProp = rootElement.GetProperty("op");
            var opCode = (OpCode)opProp.GetInt32();
            if (opCode == OpCode.Dispatch)
            {
                asyncWsMessage.IsHandled = true;
                var t = rootElement.GetProperty("t").GetString();
                var result = t == "READY";
                if (!result)
                {
                    _logger.LogWarning($"[QAPI] {opCode}: {t}");
                }
                else
                {
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
                _logger.LogError($"[QAPI] {opCode}");
                return null;
            }

            _logger.LogWarning($"[QAPI] {opCode}");
            return false;
        });

        return identifyResult;
    }

    private async Task<bool> TryResumeSessionAsync()
    {
        var resume = JsonSerializer.Serialize(new
        {
            op = 6,
            d = new
            {
                token = Authorization,
                session_id = _lastSessionId,
                seq = _lastSequence
            }
        });

        using var filter = SendMessage(resume);
        _logger.LogDebug($"[QAPI] Trying resume session");
        var resumeStatus = await filter.FilterMessageAsync(asyncWsMessage =>
        {
            var jsonDocument = JsonDocument.Parse(asyncWsMessage.Message);
            var rootElement = jsonDocument.RootElement;
            var opProp = rootElement.GetProperty("op");
            var opCode = (OpCode)opProp.GetInt32();
            if (opCode == OpCode.Dispatch)
            {
                asyncWsMessage.IsHandled = true;
                var t = rootElement.GetProperty("t").GetString();
                var result = t == "RESUMED";
                if (!result)
                {
                    _logger.LogWarning($"[QAPI] {opCode}: {t}");
                    return false;
                }
                else
                {
                    _logger.LogDebug($"[QAPI] Session resumed");
                    return true;
                }
            }

            _logger.LogWarning($"[QAPI] {opCode}");
            return false;
        });

        return resumeStatus;
    }

    private async Task KeepSendHeartbeat(CancellationToken token)
    {
        var lastSequence = default(int?);
        while (!token.IsCancellationRequested)
        {
            var ack = JsonSerializer.Serialize(new
            {
                op = 1,
                d = lastSequence
            });
            lastSequence = _lastSequence;
            using (var filter = SendMessage(ack))
            {
                _logger.LogDebug($"[QAPI] Sent heartbeat");
                await filter.FilterMessageAsync(asyncWsMessage =>
                {
                    asyncWsMessage.IsHandled = true;
                    var jsonDocument = JsonDocument.Parse(asyncWsMessage.Message);
                    var rootElement = jsonDocument.RootElement;
                    var opProp = rootElement.GetProperty("op");
                    var opCode = (OpCode)opProp.GetInt32();
                    if (opCode == OpCode.HeartbeatACK)
                    {
                        _logger.LogDebug($"[QAPI] Received heartbeat");
                        return true;
                    }

                    _logger.LogWarning($"QApi {opCode}");
                    return false;
                });
            }

            await Task.Delay(_heartBeatInterval, token);
        }
    }

    private static void ValidateResult(JsonNode jsonNode)
    {
        var code = jsonNode["code"];
        if (code != null)
        {
            var message = jsonNode["message"];
            throw new QApiException(code.GetValue<int>().ToString(), message?.GetValue<string>());
        }
    }
    //public Task ConnectAsync()
    //{
    //    if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));

    //    throw new NotImplementedException();
    //}

    //public Task DisconnectAsync()
    //{
    //    throw new NotImplementedException();
    //}

    //public Task<string> SendMessageAsync(string message, string state)
    //{
    //    throw new NotImplementedException();
    //}
}