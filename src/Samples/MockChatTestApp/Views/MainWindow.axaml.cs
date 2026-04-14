using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using MilkiBotFramework;
using MilkiBotFramework.Platforms.Mock;
using MilkiBotFramework.Platforms.Mock.Connecting;
using MilkiBotFramework.Platforms.Mock.Messaging;

namespace MockChatTestApp.Views;

/// <summary>
///     Mock 聊天测试应用 - 用于本地测试 Bot 的聊天功能
/// </summary>
public partial class MainWindow : Window
{
    // 聊天消息集合
    private readonly ObservableCollection<MockMessage> _messages = new();
    private Bot? _bot;
    private MockConnector? _connector;
    private string _currentChatMode = "group"; // "group" 或 "private"
    private bool _isBotRunning = false;
    private MockBotOptions? _mockOptions;

    public MainWindow()
    {
        InitializeComponent();
        MessageList.ItemsSource = _messages;
        MockMessageApi.MessageSent += OnMockMessageSent;
        Closed += OnWindowClosed;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        MockMessageApi.MessageSent -= OnMockMessageSent;
    }

    private void AppendMessage(MockMessage message)
    {
        _messages.Add(message);
        _ = ScrollToBottomAsync();
    }

    private async Task ScrollToBottomAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var verticalOffset = Math.Max(0, MessageScrollViewer.Extent.Height - MessageScrollViewer.Viewport.Height);
            MessageScrollViewer.Offset = new Vector(0, verticalOffset);
        }, DispatcherPriority.Background);
    }

    /// <summary>
    ///     启动 Bot
    /// </summary>
    private async void OnStartBotClick(object? sender, RoutedEventArgs e)
    {
        if (_isBotRunning)
        {
            ShowNotice("Bot is already running!");
            return;
        }

        try
        {
            MockMessageApi.ClearSentMessages();

            _bot = new BotBuilder()
                .UseMock()
                .Build();

            // 获取连接器和配置
            _connector = (MockConnector)_bot.Connector;
            _mockOptions = (MockBotOptions)_bot.Options;

            // 启动 Bot
            _isBotRunning = true;
            _ = _bot.RunAsync();

            // 添加欢迎消息
            AddSystemMessage("✓ Bot started successfully!");
            AddGroupSystemMessage("✓ Connected to Mock Test Group");
        }
        catch (Exception ex)
        {
            ShowNotice($"Error starting bot: {ex.Message}", "Error");
        }
    }

    /// <summary>
    ///     停止 Bot
    /// </summary>
    private async void OnStopBotClick(object? sender, RoutedEventArgs e)
    {
        if (!_isBotRunning || _bot == null)
        {
            ShowNotice("Bot is not running!");
            return;
        }

        try
        {
            _isBotRunning = false;
            await _bot.StopAsync();
            AddSystemMessage("✓ Bot stopped");
        }
        catch (Exception ex)
        {
            ShowNotice($"Error stopping bot: {ex.Message}", "Error");
        }
    }

    /// <summary>
    ///     选择群聊
    /// </summary>
    private void OnGroupChatSelected(object? sender, PointerPressedEventArgs e)
    {
        _currentChatMode = "group";
        _messages.Clear();
        AddGroupSystemMessage("Switched to group chat");
    }

    /// <summary>
    ///     选择私聊
    /// </summary>
    private void OnPrivateChatSelected(object? sender, PointerPressedEventArgs e)
    {
        _currentChatMode = "private";
        _messages.Clear();
        AddSystemMessage("Switched to private chat");
    }

    /// <summary>
    ///     发送消息
    /// </summary>
    private async void OnSendMessageClick(object? sender, RoutedEventArgs e)
    {
        var messageInput = this.FindControl<TextBox>("MessageInput");
        if (messageInput == null) return;

        var messageText = messageInput.Text?.Trim();
        if (string.IsNullOrEmpty(messageText))
            return;

        messageInput.Text = "";

        if (!_isBotRunning)
        {
            ShowNotice("Bot is not running! Start it first.");
            return;
        }

        if (_connector == null || _mockOptions == null)
            return;

        try
        {
            var config = _mockOptions.Config;

            // 创建用户消息
            var userMessage = new MockMessage
            {
                Id = Guid.NewGuid().ToString(),
                SenderId = config.UserId,
                SenderName = config.UserName,
                Content = messageText,
                Timestamp = DateTimeOffset.Now,
                IsBotMessage = false,
                GroupId = _currentChatMode == "group" ? config.GroupId : null,
                GroupName = _currentChatMode == "group" ? config.GroupName : null
            };

            // 显示用户消息
            AppendMessage(userMessage);

            // 模拟发送给 Bot
            await _connector.SimulateReceiveMessageAsync(userMessage);
        }
        catch (Exception ex)
        {
            ShowNotice($"Error sending message: {ex.Message}", "Error");
        }
    }

    /// <summary>
    ///     处理消息输入框的 Enter 键
    /// </summary>
    private void OnMessageInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Return)
        {
            OnSendMessageClick(null, null!);
        }
    }

    /// <summary>
    ///     清空消息
    /// </summary>
    private void OnClearMessagesClick(object? sender, RoutedEventArgs e)
    {
        _messages.Clear();
        MockMessageApi.ClearSentMessages();
    }

    /// <summary>
    ///     Mock MessageApi 发送事件处理
    /// </summary>
    private void OnMockMessageSent(MockMessage msg)
    {
        if (!ShouldDisplayInCurrentChat(msg))
        {
            return;
        }

        Dispatcher.UIThread.InvokeAsync(() => { AppendMessage(msg); }, DispatcherPriority.Normal);
    }

    private bool ShouldDisplayInCurrentChat(MockMessage msg)
    {
        if (_currentChatMode == "group")
        {
            return !string.IsNullOrWhiteSpace(msg.GroupId);
        }

        return string.IsNullOrWhiteSpace(msg.GroupId);
    }

    /// <summary>
    ///     点击图片消息后，调用系统图片查看器/浏览器打开
    /// </summary>
    private void OnImageMessageClick(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: string source } || string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        try
        {
            if (Uri.TryCreate(source, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = source,
                    UseShellExecute = true
                });
                return;
            }

            var imagePath = Path.IsPathRooted(source) ? source : Path.GetFullPath(source);
            if (!File.Exists(imagePath))
            {
                ShowNotice($"Image file not found: {imagePath}", "Error");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = imagePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowNotice($"Error opening image: {ex.Message}", "Error");
        }
    }

    /// <summary>
    ///     添加系统消息（私聊）
    /// </summary>
    private void AddSystemMessage(string text)
    {
        AppendMessage(new MockMessage
        {
            SenderName = "System",
            Content = text,
            Timestamp = DateTimeOffset.Now,
            IsBotMessage = false
        });
    }

    /// <summary>
    ///     添加系统消息（群聊）
    /// </summary>
    private void AddGroupSystemMessage(string text)
    {
        AppendMessage(new MockMessage
        {
            SenderName = "System",
            Content = text,
            Timestamp = DateTimeOffset.Now,
            IsBotMessage = false,
            GroupId = _mockOptions?.Config.GroupId,
            GroupName = _mockOptions?.Config.GroupName
        });
    }

    /// <summary>
    ///     以系统消息方式展示提示
    /// </summary>
    private void ShowNotice(string message, string title = "Info")
    {
        AddSystemMessage($"[{title}] {message}");
    }
}