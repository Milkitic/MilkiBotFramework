using System.ComponentModel;
using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Platforms.GoCqHttp;

public class GoCqConnection
{
    public GoCqConnection()
    {
    }

    public GoCqConnection(ConnectionType connectionType, string? targetUri, string? serverBindUrl, string? serverBindPath)
    {
        ConnectionType = connectionType;
        TargetUri = targetUri;
        ServerBindPath = serverBindPath;
        ServerBindUrl = serverBindUrl;
    }

    [Description("连接方式，支持: Http, WebSocket, ReverseWebSocket")]
    public ConnectionType ConnectionType { get; set; } = ConnectionType.WebSocket;

    [Description("目标接口链接。当 ConnectionType 为 Http, WebSocket 时生效")]
    public string? TargetUri { get; set; } = "ws://127.0.0.1:5700";

    [Description("服务器绑定Url。当 ConnectionType 为 Http, ReverseWebSocket 时生效")]
    public string? ServerBindUrl { get; set; } = "http://0.0.0.0:2333";

    [Description("服务器绑定的具体路由。当 ConnectionType 为 Http, ReverseWebSocket 时生效")]
    public string? ServerBindPath { get; set; } = "/endpoint";

    public static GoCqConnection WebSocket(string targetUri)
    {
        return new GoCqConnection(ConnectionType.WebSocket, targetUri, null, null);
    }

    public static GoCqConnection ReverseWebSocket(string serverBindUrl, string serverBindPath)
    {
        return new GoCqConnection(ConnectionType.ReverseWebSocket, null, serverBindUrl, serverBindPath);
    }

    public static GoCqConnection Http(string targetUri, string serverBindUrl, string serverBindPath)
    {
        return new GoCqConnection(ConnectionType.Http, targetUri, serverBindUrl, serverBindPath);
    }
}