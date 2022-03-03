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

    public static GoCqConnection Websocket(string callingUri)
    {
        return new GoCqConnection(ConnectionType.Websocket, callingUri, null);
    }

    public static GoCqConnection ReverseWebsocket(string serverBindPath)
    {
        return new GoCqConnection(ConnectionType.ReverseWebsocket, null, serverBindPath);
    }

    public static GoCqConnection Http(string callingUri, string serverBindPath)
    {
        return new GoCqConnection(ConnectionType.Http, callingUri, serverBindPath);
    }
}