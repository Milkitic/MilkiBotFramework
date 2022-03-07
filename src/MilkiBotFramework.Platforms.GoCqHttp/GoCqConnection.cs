using MilkiBotFramework.Connecting;

namespace MilkiBotFramework.Platforms.GoCqHttp;

public class GoCqConnection
{
    private GoCqConnection(ConnectionType connectionType, string? targetUri, string? serverBindPath)
    {
        ConnectionType = connectionType;
        TargetUri = targetUri;
        ServerBindPath = serverBindPath;
    }

    public ConnectionType ConnectionType { get; private set; }
    public string? TargetUri { get; private set; }
    public string? ServerBindPath { get; private set; }

    public static GoCqConnection WebSocket(string callingUri)
    {
        return new GoCqConnection(ConnectionType.WebSocket, callingUri, null);
    }

    public static GoCqConnection ReverseWebSocket(string serverBindPath)
    {
        return new GoCqConnection(ConnectionType.ReverseWebSocket, null, serverBindPath);
    }

    public static GoCqConnection Http(string callingUri, string serverBindPath)
    {
        return new GoCqConnection(ConnectionType.Http, callingUri, serverBindPath);
    }
}