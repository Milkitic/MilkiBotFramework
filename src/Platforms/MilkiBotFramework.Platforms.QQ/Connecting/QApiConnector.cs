using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using MilkiBotFramework.Aspnetcore;

namespace MilkiBotFramework.Platforms.QQ.Connecting;

public class QApiConnector : AspnetcoreConnector
{
    private const string ProductHost = "api.sgroup.qq.com";
    private const string SandboxHost = "sandbox.api.sgroup.qq.com";

    private DateTime _tokenExpireTime;
    private string? _accessToken;

    private int _lastSequence;
    private Guid _lastSessionId;

    public QApiConnector(ILogger<QApiConnector> logger, WebApplication webApplication)
        : base(null, logger, webApplication)
    {
    }


    public QConnection Connection { get; internal set; }
    public override string BindingPath
    {
        get => Connection.CallbackPath;
        set => Connection.CallbackPath = value;
    }

    public string Host
    {
        get
        {
            if (Connection == null) throw new ArgumentNullException(nameof(Connection), default(string));
            return Connection.IsDevelopment ? SandboxHost : ProductHost;
        }
    }

    public string Authorization => $"QQBot {_accessToken}";
    public int MessageSequence => _lastSequence;
}