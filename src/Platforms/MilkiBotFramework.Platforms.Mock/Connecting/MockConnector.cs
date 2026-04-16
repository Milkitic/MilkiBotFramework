using System.Text;
using MilkiBotFramework.Connecting;
using MilkiBotFramework.Platforms.Mock.Messaging;

namespace MilkiBotFramework.Platforms.Mock.Connecting;

/// <summary>
///     Mock 虚拟连接器 - 用于本地测试，不需要真实网络连接
/// </summary>
public class MockConnector : IConnector
{
    private readonly MockBotOptions _options;
    private bool _connected = false;

    public MockConnector(BotOptions options)
    {
        if (options is not MockBotOptions mockOptions)
        {
            throw new ArgumentException("Options must be of type MockBotOptions", nameof(options));
        }

        _options = mockOptions;
        OnMessageSimulated += HandleSimulatedMessage;
    }

    public event Func<InboundMessage, Task>? MessageReceived;

    public string? TargetUri { get; set; }
    public string? BindingPath { get; set; }
    public ConnectionType ConnectionType { get; set; }
    public TimeSpan ErrorReconnectTimeout { get; set; }
    public TimeSpan MessageTimeout { get; set; }
    public Encoding? Encoding { get; set; }

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _connected = true;
        await Task.Delay(100, cancellationToken); // 模拟连接延迟
    }

    public async Task DisconnectAsync()
    {
        _connected = false;
        await Task.Delay(100);
    }

    public async Task<string> SendMessageAsync(string message, string state)
    {
        if (!_connected)
            throw new InvalidOperationException("Mock connector is not connected");

        // Mock 平台通常不需要通过 SendMessageAsync 发送，而是通过 MessageApi
        // 这里只是作为兼容接口返回状态
        await Task.Delay(50);
        return "mock_msg_" + Guid.NewGuid().ToString().Substring(0, 8);
    }

    /// <summary>
    ///     用于外部模拟发送消息
    /// </summary>
    public static event Func<MockMessage, Task>? OnMessageSimulated;

    /// <summary>
    ///     供外部模拟器调用，用来模拟接收消息
    /// </summary>
    public async Task SimulateReceiveMessageAsync(MockMessage message)
    {
        if (!_connected)
            throw new InvalidOperationException("Mock connector is not connected");

        if (MessageReceived != null)
        {
            await MessageReceived.Invoke(InboundMessage.FromPayload(message, message.Content, "mock"));
        }
    }

    private async Task HandleSimulatedMessage(MockMessage message)
    {
        if (_connected)
        {
            await SimulateReceiveMessageAsync(message);
        }
    }
}