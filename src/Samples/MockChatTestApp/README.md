# Mock Chat Test Application

这是一个基于 MilkiBotFramework 的本地测试工具，用于在不需要真实平台连接的情况下，测试和演示 Bot 的聊天功能。

## 功能特性

- **虚拟平台**：无需外部服务，本地运行完整的 Bot 实例
- **简洁的聊天 UI**：基于 Avalonia 构建的跨平台聊天界面
- **群聊和私聊模拟**：支持两种不同的聊天场景
- **实时消息交互**：模拟用户发送消息，观察 Bot 响应
- **开箱即用**：预配置了示例插件和虚拟联系人

## 快速开始

### 1. 构建并运行

```bash
cd src/Samples/MockChatTestApp
dotnet build
dotnet run
```

### 2. 启动 Bot

点击窗口顶部的 "Start Bot" 按钮，Bot 将初始化并连接到虚拟平台。

### 3. 发送消息

1. 选择左侧的对话（群聊或私聊）
2. 在底部输入框中输入消息
3. 按 Enter 或点击 "Send" 按钮发送

### 4. 与 Bot 交互

尝试这些命令：

```
/help              - 显示帮助信息
/echo hello        - 回显消息
/hello World       - 问候示例
/time              - 显示当前时间
/ping              - Ping/Pong
/count 10          - 计数演示（异步响应）
```

## 架构说明

### Mock Platform 组件

Mock Platform 位于 `src/Platforms/MilkiBotFramework.Platforms.Mock/`，包含：

- **MockConnector**：虚拟连接器，不依赖外部网络
- **MockDispatcher**：分发虚拟消息到业务逻辑
- **MockMessageContext**：虚拟消息上下文
- **MockMessageApi**：虚拟消息 API（用于发送）
- **MockContactsManager**：虚拟联系人管理（预置固定群组和私聊）

### 虚拟数据

默认配置：
- **群聊 ID**：`mock_group_001`
- **群聊名称**：`Mock Test Group`
- **用户 ID**：`mock_user_001`
- **用户名**：`Mock User`
- **Bot ID**：`mock_bot_001`
- **Bot 名称**：`Mock Bot`

这些配置可以在 `appsettings.yaml` 中修改。

### 示例插件

`MockChatDemoPlugin` 演示了如何：
1. 使用命令处理器 (`[CommandHandler]`)
2. 处理消息上下文
3. 实现异步响应
4. 自动回复特定关键词

## 扩展使用

### 自定义插件

你可以在 `Plugins/` 文件夹中添加自己的插件。示例：

```csharp
using MilkiBotFramework.Messaging;
using MilkiBotFramework.Plugining;
using MilkiBotFramework.Plugining.Attributes;

[PluginIdentifier(guid: "your-guid", name: "Your Plugin")]
public class YourPlugin : BasicPlugin
{
    [CommandHandler]
    public IResponse YourCommand([Argument] string arg)
        => Reply($"Your response: {arg}");
}
```

### 修改虚拟数据

编辑或创建 `appsettings.yaml`：

```yaml
# 这里可以配置 Mock 平台选项
# 更多配置见 MockBotOptions.cs
```

### 集成到实际项目

Mock Platform 可以在以下场景中使用：

1. **单元测试**：在不启动真实平台的情况下测试插件逻辑
2. **开发演示**：快速展示功能而无需配置外部服务
3. **CI/CD 集成**：自动化测试和验证
4. **学习教程**：新手入门时的实验环境

## 常见问题

### Q: 我需要修改虚拟用户或群组吗？
A: 虚拟数据是硬编码的，修改需要编辑 `MockContactsManager.cs`。如果需要动态修改，可以将其改为从配置文件加载。

### Q: 能否测试多个 Bot?
A: 可以！在 MainWindow 中启动多个 BotBuilder 实例，每个实例会有独立的运行时。

### Q: 如何追踪 Bot 的处理过程？
A: 启用日志记录。Console 输出会显示详细的处理流程，包括插件加载、消息分发、命令解析等。

## 相关文件

- `Views/MainWindow.axaml` - UI 布局
- `Views/MainWindow.axaml.cs` - UI 交互逻辑
- `Plugins/MockChatDemoPlugin.cs` - 示例插件
- `Program.cs` - 应用入口

## 许可证

本项目采用 GPL-3.0 License 开源。
