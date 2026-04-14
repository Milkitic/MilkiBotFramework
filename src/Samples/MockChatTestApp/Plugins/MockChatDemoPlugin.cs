using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Messaging.RichMessages;
using MilkiBotFramework.Platforms.Mock.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;

namespace MockChatTestApp.Plugins;

/// <summary>
///     Mock Chat Test Bot 示例插件 - 演示如何响应虚拟消息
/// </summary>
[PluginIdentifier(guid: "a1b2c3d4-e5f6-7890-abcd-ef1234567890", name: "Mock Chat Demo Plugin")]
public class MockChatDemoPlugin : BasicPlugin<MockMessageContext>
{
    [CommandHandler]
    public IResponse Echo([Argument] string message)
        => Reply($"Echo: {message}");

    [CommandHandler]
    public IResponse Hello([Argument(DefaultValue = "Friend")] string name)
        => Reply($"Hello, {name}! This is a mock bot test.");

    [CommandHandler]
    public IResponse Time()
        => Reply($"Current time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

    [CommandHandler]
    public IResponse Help()
    {
        var helpText = @"Available commands:
/echo <message> - Echo your message
/hello [name] - Greet someone
/time - Show current time
/image <pathOrUrl> - Send image by local path or http/https url
/count [number] - Count to a number
/ping - Pong!";
        return Reply(helpText);
    }

    [CommandHandler]
    public IResponse Image([Argument] string pathOrUrl)
    {
        if (Uri.TryCreate(pathOrUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return Reply(new LinkImage(pathOrUrl));
        }

        var fullPath = Path.IsPathRooted(pathOrUrl) ? pathOrUrl : Path.GetFullPath(pathOrUrl);
        if (!File.Exists(fullPath))
        {
            return Reply($"Image file not found: {fullPath}");
        }

        return Reply(new FileImage(fullPath));
    }

    [CommandHandler]
    public async IAsyncEnumerable<IResponse> Count([Argument(DefaultValue = "5")] int number)
    {
        for (int i = 1; i <= number; i++)
        {
            yield return Reply($"Count: {i}/{number}");
            await Task.Delay(500); // 延迟以模拟处理
        }
    }

    [CommandHandler]
    public IResponse Ping()
        => Reply("Pong!");

    public override async IAsyncEnumerable<IResponse> OnMessageReceived(MockMessageContext context)
    {
        // 如果收到的消息包含特定关键词，可以自动回复
        if (context.TextMessage?.Contains("test", StringComparison.OrdinalIgnoreCase) == true)
        {
            yield return Reply("Test mode detected!");
        }

        // 调用基类方法以继续正常命令处理
        await foreach (var response in base.OnMessageReceived(context))
        {
            yield return response;
        }
    }
}