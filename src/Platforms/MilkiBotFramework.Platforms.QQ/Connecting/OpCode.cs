namespace MilkiBotFramework.Platforms.QQ.Connecting;

/// <summary>QQ 网关和 HTTP 回调共用的载荷操作码。</summary>
// ReSharper disable InconsistentNaming
public enum OpCode
{
    /// <summary>平台下发事件。</summary>
    Dispatch = 0,
    /// <summary>客户端发送网关心跳。</summary>
    Heartbeat = 1,
    /// <summary>客户端提交网关登录鉴权。</summary>
    Identify = 2,
    /// <summary>客户端请求恢复已建立的网关会话。</summary>
    Resume = 6,
    /// <summary>平台通知客户端重连。</summary>
    Reconnect = 7,
    /// <summary>平台告知网关会话无效。</summary>
    InvalidSession = 9,
    /// <summary>网关连接建立后下发心跳间隔。</summary>
    Hello = 10,
    /// <summary>平台确认收到网关心跳。</summary>
    HeartbeatACK = 11,
    /// <summary>机器人确认收到 HTTP 回调。</summary>
    HTTPCallbackACK = 12,
    /// <summary>平台请求验证 HTTP 回调地址。</summary>
    Validate = 13
}
