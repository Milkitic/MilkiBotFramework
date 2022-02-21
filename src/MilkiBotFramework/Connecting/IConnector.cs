using System;
using System.Text;
using System.Threading.Tasks;

namespace MilkiBotFramework.Connecting;

public interface IConnector : IConnectorConfigurable
{
    event Func<string, Task>? RawMessageReceived;
    Task ConnectAsync();
    Task DisconnectAsync();
    Task<string> SendMessageAsync(string message, string state);
}

public interface IConnectorConfigurable
{
    public string ServerUri { get; set; }
    public TimeSpan ConnectionTimeout { get; set; }

    /// <summary>
    /// 消息超时时间。
    /// 对于一些长消息超时的情况，请适量增大此值。
    /// </summary>
    public TimeSpan MessageTimeout { get; set; }

    public Encoding Encoding { get; set; }
}