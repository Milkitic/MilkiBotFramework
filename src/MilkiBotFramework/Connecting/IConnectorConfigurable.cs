using System;
using System.Text;

namespace MilkiBotFramework.Connecting;

public interface IConnectorConfigurable
{
    public ConnectionType ConnectionType { get; set; }
    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public TimeSpan ConnectionTimeout { get; set; }

    /// <summary>
    /// 消息超时时间。
    /// 对于一些长消息超时的情况，请适量增大此值。
    /// </summary>
    public TimeSpan MessageTimeout { get; set; }

    public Encoding Encoding { get; set; }
}